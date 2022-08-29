namespace System.Linq.DynamicQuery;

internal sealed class FilterParser {
    private Token _currentToken;

    private static readonly MethodInfo _startsWith = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
    private static readonly MethodInfo _endsWith = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
    private static readonly MethodInfo _contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

    private static readonly PropertyInfo _stringIndexer = typeof(string).GetProperty("Chars")!;

    private static readonly IReadOnlyDictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo> {
        ["MAX"] = typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(int), typeof(int) })!,
        ["MIN"] = typeof(Math).GetMethod(nameof(Math.Min), new[] { typeof(int), typeof(int) })!,
    };

    private FilterParser(string input) {
        _currentToken = Lexer.ReadFrom(input);
    }

    public static Expression ParseFor<TOutput>(string input, Expression instance) {
        return ParseFor(input, instance, typeof(TOutput));
    }

    internal static Expression ParseFor(string input, Expression instance, Type outputType) {
        var builder = new FilterParser(input);
        var startToken = builder._currentToken;
        var root = builder.GetNodeTree();
        root = BalanceTree(root);
        var result = TransformTree(instance, root);
        EnsureType(result, startToken, "result of the expression", outputType);
        return result;
    }

    private TreeNode GetNodeTree(string? operation = null) {
        var node = GetNode();
        while (TryMoveNext()) {
            if (_currentToken is SymbolToken token) {
                if (operation == "Scope" && token.Symbol == ")") break;
                if (operation == "Argument" && token.Symbol is ")" or ",") break;
                if (operation == "Index" && token.Symbol == "]") break;
                if (operation == "Between" && token.Symbol == "AND") break;
            }
            node = GetNode(node);
        }
        return node;
    }

    private TreeNode GetNode(TreeNode? previous = null) {
        return _currentToken switch {
            SymbolToken symbol => CreateOperationNode(symbol, previous),
            NamedToken named when _currentToken.Next is SymbolToken { Symbol: "(" } => CreateCallNode(named),
            NamedToken named => CreateFieldNode(named),
            _ => CreateValueNode((ValueToken)_currentToken)
        };
    }

    private TreeNode CreateCallNode(Token token) {
        if (token.Previous is not null && token.Previous is not SymbolToken) throw new FilterException(token);
        if (token.Previous is SymbolToken { Symbol: "]" or ")"}) throw new FilterException(token);
        var node = new TreeNode(token, 0);
        return AddNodeArguments(node);
    }

    private TreeNode CreateFieldNode(Token token) {
        if (token.Previous is not null && token.Previous is not SymbolToken) throw new FilterException(token);
        if (token.Previous is SymbolToken { Symbol: "]" or ")" }) throw new FilterException(token);
        var node = new TreeNode(token, 0, true);
        return token.Next is SymbolToken { Symbol: "[" } ? AddNodeIndexes(node) : node;
    }

    private TreeNode CreateValueNode(Token token) {
        if (token.Previous is not null && token.Previous is not SymbolToken) throw new FilterException(token);
        if (token.Previous is SymbolToken { Symbol: "]" or ")" }) throw new FilterException(token);
        var node = new TreeNode(token, 0);
        return token.Next is SymbolToken { Symbol: "[" } ? AddNodeIndexes(node) : node;
    }

    private TreeNode AddNodeIndexes(TreeNode node) {
        MoveNext();
        node.Children.Add(GetNodeIndex());
        return node;
    }

    private TreeNode AddNodeArguments(TreeNode node) {
        MoveNext();
        GetChildrenFor(node);
        return node;
    }

    private TreeNode CreateOperationNode(SymbolToken token, TreeNode? previous) {
        return token.Symbol switch {
            "(" when token.Previous is null or SymbolToken => CreateScopeNode(),
            "+" or "-" when token.Previous is null or SymbolToken => CreateUnaryNode(new SymbolToken(token.Position, $"[{token.Symbol}]"), 1),
            "NOT" when token.Previous is null or SymbolToken => CreateUnaryNode(token, 1),
            "^" => CreateBinaryNode(token, previous, 2),
            "*" or "/" or "%" => CreateBinaryNode(token, previous, 3),
            "+" or "-" => CreateBinaryNode(token, previous, 4),
            "<" or ">" or "<=" or ">=" => CreateBinaryNode(token, previous, 5),
            "=" or "<>" => CreateBinaryNode(token, previous, 5),
            "CONTAINS" or "STARTSWITH" or "ENDSWITH" => CreateBinaryNode(token, previous, 5),
            "BETWEEN" => CreateBetweenNode(token, previous, 5),
            "IS" => CreateBinaryNode(token, previous, 6),
            "AND" => CreateBinaryNode(token, previous, 7),
            "OR" => CreateBinaryNode(token, previous, 8),
            "IN" => CreateChoiceNode(token, previous),
            _ => throw new FilterException(token),
        };
    }

    private TreeNode CreateScopeNode() {
        MoveNext(); // skip (
        var tree = GetNodeTree("Scope");
        return _currentToken is SymbolToken { Symbol: ")" }
            ? tree
            : throw new FilterException(_currentToken);
    }

    private TreeNode CreateUnaryNode(Token token, int precedence) {
        var node = new TreeNode(token, precedence);
        MoveNext();
        var child = GetNode();
        node.Children.Add(child);
        return node;
    }

    private TreeNode CreateBinaryNode(Token token, TreeNode? left, int precedence) {
        var node = new TreeNode(token, precedence);
        if (left is null) throw new FilterException(token);
        node.Children.Add(left);
        MoveNext();
        var right = GetNode();
        node.Children.Add(right);
        return node;
    }

    private TreeNode CreateBetweenNode(Token token, TreeNode? left, int precedence) {
        var node = new TreeNode(token, precedence);
        if (left is null) throw new FilterException(token);
        node.Children.Add(left);
        MoveNext();
        var first = GetNodeTree("Between");
        node.Children.Add(first);
        if (_currentToken is not SymbolToken { Symbol: "AND" }) throw new FilterException(_currentToken);
        MoveNext();
        var second = GetNode();
        node.Children.Add(second);
        return node;
    }

    private TreeNode CreateChoiceNode(Token token, TreeNode? left) {
        var node = new TreeNode(token, 0);
        if (left is null) throw new FilterException(token);
        node.Children.Add(left);
        MoveNext();
        GetChildrenFor(node);
        return node;
    }

    private void GetChildrenFor(TreeNode node) {
        do node.Children.Add(GetChild());
        while (HasAnotherArgument());
    }

    private TreeNode GetChild() {
        MoveNext();
        return GetNodeTree("Argument");
    }

    private static TreeNode BalanceTree(TreeNode node) {
        if (node.Token is not SymbolToken) return node;
        node = BalanceNode(node);
        for (var i = 0; i < node.Children.Count; i++) {
            node.Children[i] = BalanceTree(node.Children[i]);
        }
        return node;
    }

    private static TreeNode BalanceNode(TreeNode node) {
        while (node.Children[0].Precedence > node.Precedence) {
            var top = node.Children[0];
            var acc = top.Children[^1];
            top.Children[^1] = node;
            node.Children[0] = acc;
            node = top;
        }

        return node;
    }

    private static Expression TransformTree(Expression instance, TreeNode node) {
        var children = node.Children.Select(n => TransformTree(instance, n)).ToArray();
        var tokens = node.Children.Select(i => i.Token).ToArray();
        return node.Token switch {
            ValueToken valueToken => CreateValueExpression(valueToken.Value, node, children),
            NamedToken namedToken when node.IsField => CreateFieldExpression(instance, namedToken.Name, node, children),
            NamedToken namedToken => CreateCallExpression(namedToken.Name, node, children),
            _ => CreateOperatorExpression(((SymbolToken)node.Token).Symbol, tokens, children)
        };
    }

    private static Expression CreateValueExpression(object? value, TreeNode node, IReadOnlyList<Expression> children) {
        var result = Expression.Constant(value);
        if (children.Count == 0) return result;

        EnsureType(result, node.Token, "indexed value", typeof(string));
        EnsureType(children[0], node.Children[0].Token, "index", typeof(int));
        return Expression.MakeIndex(result, _stringIndexer, new[] { children[0] });
    }

    private static Expression CreateFieldExpression(Expression instance, string name, TreeNode node, IReadOnlyList<Expression> children) {
        if (instance.Type.GetProperties().All(p => p.Name != name)) throw new FilterException(node.Token, $"'{name}' is not a public member of '{instance.Type.Name}'.");
        var field = Expression.PropertyOrField(instance, name);
        if (children.Count == 0) return field;

        EnsureType(field, node.Token, "indexed field", typeof(string));
        EnsureType(children[0], node.Children[0].Token, "index", typeof(int));
        return Expression.MakeIndex(field, _stringIndexer, new[] { children[0] });
    }

    private static Expression CreateCallExpression(string name, TreeNode node, IReadOnlyList<Expression> children) {
        var method = GetMethodByName(name.ToUpper(), node.Token);
        return Expression.Call(null, method, children);
    }

    private static MethodInfo GetMethodByName(string name, Token token) {
#pragma warning disable IDE0046 // Convert to conditional expression
        if (_methods.ContainsKey(name)) return _methods[name];
        throw new FilterException(token,$"Method '{name}' not supported.");
#pragma warning restore IDE0046 // Convert to conditional expression
    }

    private static Expression CreateOperatorExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        return symbol switch {
            "[+]" or "[-]" => CreateUnaryExpression(symbol, tokens[0], children[0]),
            "NOT" => CreateNotExpression(tokens[0], children[0]),
            "BETWEEN" => CreateBetweenExpression(tokens, children),
            "IN" => CreateChoiceExpression(tokens, children),
            "IS" => CreateIsExpression(tokens, children),
            "CONTAINS" or "STARTSWITH" or "ENDSWITH" => CreateTextExpression(symbol, tokens, children),
            "^" => CreatePowerExpression(symbol, tokens, children),
            "*" or "/" or "%" or "+" or "-" => CreateMathExpression(symbol, tokens, children),
            "<" or ">" or "<=" or ">=" => CreateCompareExpression(symbol, tokens, children),
            "=" or "<>" => CreateEqualityExpression(symbol, tokens, children),
            _ => CreateBooleanExpression(symbol, tokens, children),
        };
    }

    private static Expression CreateUnaryExpression(string symbol, Token token, Expression operand) {
        EnsureType(operand, token, "expression", typeof(int), typeof(double));
        return symbol switch {
            "[-]" => Expression.Negate(operand),
            _ => operand,
        };
    }
    private static Expression CreateNotExpression(Token token, Expression operand) {
        EnsureType(operand, token, "expression", typeof(bool));
        return Expression.Not(operand);
    }

    private static Expression CreateBetweenExpression(IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(int), typeof(double), typeof(char));
        EnsureType(children[1], tokens[1], "lower limit", children[0].Type);
        EnsureType(children[2], tokens[2], "upper limit", children[0].Type);
        return Expression.And(
            Expression.GreaterThanOrEqual(children[0], children[1]),
            Expression.LessThanOrEqual(children[0], children[2]));
    }

    private static Expression CreateChoiceExpression(IReadOnlyList<Token> tokens, IList<Expression> children) {
        var choices = (Expression)Expression.Constant(false);
        for (var i = 1; i < children.Count; i++) {
            EnsureType(children[i], tokens[i], "argument", children[0].Type);
            choices = Expression.Or(choices, Expression.Equal(children[0], children[i]));
        }
        return choices;
    }

    private static Expression CreateIsExpression(IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(bool));
        EnsureType(children[1], tokens[1], "value on the right", children[0].Type);
        return Expression.Equal(children[0], children[1]);
    }

    private static Expression CreateTextExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(string));
        EnsureType(children[1], tokens[1], "value on the right", children[0].Type);
        return symbol switch {
            "STARTSWITH" => Expression.Call(children[0], _startsWith, children[1]),
            "ENDSWITH" => Expression.Call(children[0], _endsWith, children[1]),
            _ => Expression.Call(children[0], _contains, children[1]),
        };
    }

    private static Expression CreatePowerExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(int), typeof(double));
        EnsureType(children[1], tokens[1], "value on the right", typeof(int), typeof(double));
        if (children[0].Type == typeof(int)) children[0] = Expression.Convert(children[0], typeof(double));
        if (children[1].Type == typeof(int)) children[1] = Expression.Convert(children[1], typeof(double));
        return CreateBinaryExpression(symbol, children[0], children[1]);
    }

    private static Expression CreateMathExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(int), typeof(double));
        EnsureType(children[1], tokens[1], "value on the right", typeof(int), typeof(double));
        if (children[0].Type == typeof(int) && children[1].Type == typeof(double)) children[0] = Expression.Convert(children[0], typeof(double));
        if (children[1].Type == typeof(int) && children[0].Type == typeof(double)) children[1] = Expression.Convert(children[1], typeof(double));
        return CreateBinaryExpression(symbol, children[0], children[1]);
    }

    private static Expression CreateCompareExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(int), typeof(double), typeof(char));
        EnsureType(children[1], tokens[1], "value on the right", children[0].Type);
        return CreateBinaryExpression(symbol, children[0], children[1]);
    }

    private static Expression CreateEqualityExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[1], tokens[1], "value on the right", children[0].Type);
        return CreateBinaryExpression(symbol, children[0], children[1]);
    }

    private static Expression CreateBooleanExpression(string symbol, IReadOnlyList<Token> tokens, IList<Expression> children) {
        EnsureType(children[0], tokens[0], "value on the left", typeof(bool));
        EnsureType(children[1], tokens[1], "value on the right", children[0].Type);
        return CreateBinaryExpression(symbol, children[0], children[1]);
    }

    private static Expression CreateBinaryExpression(string operation, Expression left, Expression right) {
        return operation switch {
            "^" => Expression.Power(left, right),
            "*" => Expression.Multiply(left, right),
            "/" => Expression.Divide(left, right),
            "%" => Expression.Modulo(left, right),
            "+" => Expression.Add(left, right),
            "-" => Expression.Subtract(left, right),
            "<" => Expression.LessThan(left, right),
            ">" => Expression.GreaterThan(left, right),
            "=" => Expression.Equal(left, right),
            "<=" => Expression.LessThanOrEqual(left, right),
            ">=" => Expression.GreaterThanOrEqual(left, right),
            "<>" => Expression.NotEqual(left, right),
            "AND" => Expression.And(left, right),
            _ => Expression.Or(left, right),
        };
    }

    private static void EnsureType(Expression expression, Token token, string source, params Type[] types) {
        if (!types.Contains(expression.Type))
            throw new FilterException(token, $"The {source} must be a {string.Join(" or a ", types.Select(t => t.Name))}.");
    }

    private TreeNode GetNodeIndex() {
        MoveNext(); // Skip '['
        return GetNodeTree("Index");
    }

    private bool HasAnotherArgument() {
        return _currentToken switch {
            SymbolToken { Symbol: "," } => true,
            SymbolToken { Symbol: ")" } => false,
            _ => throw new FilterException(_currentToken)
        };
    }

    private void MoveNext() {
        if (!TryMoveNext()) throw new FilterException(_currentToken);
    }

    private bool TryMoveNext() {
        if (_currentToken.Next is null) return false;
        _currentToken = _currentToken.Next;
        return true;
    }

    internal record TreeNode {
        public TreeNode(Token token, int precedence, bool isField = false) {
            Token = token;
            Precedence = precedence;
            IsField = isField;
        }

        public bool IsField { get; }
        public Token Token { get; }
        public int Precedence { get; }
        public IList<TreeNode> Children { get; } = new List<TreeNode>();
    }
}