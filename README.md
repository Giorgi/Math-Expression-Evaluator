Math Expression Evaluator is a library for evaluating simple mathematical expressions. It supports simple expressions such as `2.5+5.9`, `17.89-2.47+7.16`, `5/2/2+1.5*3+4.58`, expressions with parentheses `(((9-6/2)*2-4)/2-6-1)/(2+24/(2+4))` and expressions with variables:

``` csharp

var a = 6;
var b = 4.32m;
var c = 24.15m;
Assert.That(engine.Evaluate("(((9-a/2)*2-b)/2-a-1)/(2+c/(2+4))",new { a, b, c}), 
            Is.EqualTo((((9 - a / 2) * 2 - b) / 2 - a - 1) / (2 + c / (2 + 4))));
```
It is also possible to specify variables by using named arguments like this:

``` csharp

dynamic dynamicEngine = new ExpressionEvaluator();

var a = 6;
var b = 4.5m;
var c = 2.6m;
Assert.That(dynamicEngine.Evaluate("(c+b)*a", a: 6, b: 4.5, c: 2.6),
            Is.EqualTo((c + b) * a));
```