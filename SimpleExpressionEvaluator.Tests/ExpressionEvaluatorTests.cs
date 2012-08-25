using System;

using NUnit.Framework;

namespace SimpleExpressionEvaluator.Tests
{
    public class ExpressionEvaluatorTests
    {
        private ExpressionEvaluator _engine;
        private Random _generator;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _engine = new ExpressionEvaluator();
            _generator = new Random();
        }

        [Test]
        public void Empty_String_Is_Zero()
        {
            Assert.That(_engine.Compile("").Execute(), Is.EqualTo(0));
        }

        [Test]
        public void Decimal_Is_Treated_As_Decimal()
        {
            var left = _generator.Next(1, 100);

            Assert.That(_engine.Compile(left.ToString()).Execute(), Is.EqualTo(left));
        }

        [Test]
        public void Two_Plus_Two_Is_Four()
        {
            Assert.That(_engine.Compile("2+2").Execute(), Is.EqualTo(4));
        }

        [Test]
        public void Can_Add_Two_Decimal_Numbers()
        {
            Assert.That(_engine.Compile("2.7+3.2").Execute(), Is.EqualTo(2.7m + 3.2m));
        }

        [Test]
        public void Can_Add_Many_Numbers()
        {
            Assert.That(_engine.Compile("1.2+3.4+5.6+7.8").Execute(), Is.EqualTo(1.2m + 3.4m + 5.6m + 7.8m));
            Assert.That(_engine.Compile("1.7+2.9+14.24+6.58").Execute(), Is.EqualTo(1.7m + 2.9m + 14.24m + 6.58m));
        }

        [Test]
        public void Can_Subtract_Two_Numbers()
        {
            Assert.That(_engine.Compile("5-2").Execute(), Is.EqualTo(5 - 2));
        }

        [Test]
        public void Can_Subtract_Multiple_Numbers()
        {
            Assert.That(_engine.Compile("15.2-2.3-4.8-0.58").Execute(), Is.EqualTo(15.2m - 2.3m - 4.8m - 0.58m));
        }

        [Test]
        public void Can_Add_And_Subtract_Multiple_Numbers()
        {
            Assert.That(_engine.Compile("15+8-4-2+7").Execute(), Is.EqualTo(15 + 8 - 4 - 2 + 7));
            Assert.That(_engine.Compile("17.89-2.47+7.16").Execute(), Is.EqualTo(17.89m - 2.47m + 7.16m));

        }

        [Test]
        public void Can_Add_Subtract_Multiply_Divide_Multiple_Numbers()
        {
            Assert.That(_engine.Compile("50-5*3*2+7").Execute(), Is.EqualTo(50 - 5 * 3 * 2 + 7));
            Assert.That(_engine.Compile("84+15+4-4*3*9+24+4-54/3-5-7+47").Execute(), Is.EqualTo(84 + 15 + 4 - 4 * 3 * 9 + 24 + 4 - 54 / 3 - 5 - 7 + 47));
            Assert.That(_engine.Compile("50-48/4/3+7*2*4+2+5+8").Execute(), Is.EqualTo(50 - 48 / 4 / 3 + 7 * 2 * 4 + 2 + 5 + 8));
            Assert.That(_engine.Compile("5/2/2+1.5*3+4.58").Execute(), Is.EqualTo(5 / 2m / 2m + 1.5m * 3m + 4.58m));
            Assert.That(_engine.Compile("25/3+1.34*2.56+1.49+2.36/1.48").Execute(), Is.EqualTo(25 / 3m + 1.34m * 2.56m + 1.49m + 2.36m / 1.48m));
            Assert.That(_engine.Compile("2*3+5-4-2*5+7").Execute(), Is.EqualTo(2 * 3 + 5 - 4 - 2 * 5 + 7));
        }

        [Test]
        public void Supports_Parentheses()
        {
            Assert.That(_engine.Compile("2*(5+3)").Execute(), Is.EqualTo(2 * (5 + 3)));
            Assert.That(_engine.Compile("(5+3)*2").Execute(), Is.EqualTo((5 + 3) * 2));
            Assert.That(_engine.Compile("(5+3)*5-2").Execute(), Is.EqualTo((5 + 3) * 5 - 2));
            Assert.That(_engine.Compile("(5+3)*(5-2)").Execute(), Is.EqualTo((5 + 3) * (5 - 2)));
            Assert.That(_engine.Compile("((5+3)*3-(8-2)/2)/2").Execute(), Is.EqualTo(((5 + 3) * 3 - (8 - 2) / 2) / 2m));
            Assert.That(_engine.Compile("(4*(3+5)-4-8/2-(6-4)/2)*((2+4)*4-(8-5)/3)-5").Execute(), Is.EqualTo((4 * (3 + 5) - 4 - 8 / 2 - (6 - 4) / 2) * ((2 + 4) * 4 - (8 - 5) / 3) - 5));
            Assert.That(_engine.Compile("(((9-6/2)*2-4)/2-6-1)/(2+24/(2+4))").Execute(), Is.EqualTo((((9 - 6 / 2) * 2 - 4) / 2m - 6 - 1) / (2 + 24 / (2 + 4))));
        }

        [Test]
        public void Can_Process_Simple_Variables()
        {
            decimal a = 2.6m;
            decimal b = 5.7m;

            Assert.That(_engine.Compile("a").Execute(new { a }), Is.EqualTo(a));
            Assert.That(_engine.Compile("a+a").Execute(new { a }), Is.EqualTo(a + a));
            Assert.That(_engine.Compile("a+b").Execute(new { a, b }), Is.EqualTo(a + b));
        }

        [Test]
        public void Can_Process_Multiple_Variables()
        {
            var a = 6;
            var b = 4.5m;
            var c = 2.6m;
            Assert.That(_engine.Compile("(((9-a/2)*2-b)/2-a-1)/(2+c/(2+4))").Execute(new { a, b, c }), Is.EqualTo((((9 - a / 2) * 2 - b) / 2 - a - 1) / (2 + c / (2 + 4))));
            Assert.That(_engine.Compile("(c+b)*a").Execute(new { a, b, c }), Is.EqualTo((c + b) * a));
        }

        [Test]
        public void Can_Pass_Named_Variables()
        {
            dynamic dynamicEngine = new ExpressionEvaluator();

            var a = 6;
            var b = 4.5m;
            var c = 2.6m;

            Assert.That(dynamicEngine.Compile("(c+b)*a").Execute(a: 6, b: 4.5, c: 2.6), Is.EqualTo((c + b) * a));
        }

        [Test]
        public void Can_Invoke_Expression_Multiple_Times()
        {
            var a = 6m;
            var b = 3.9m;
            var c = 4.9m;

            var compiled = _engine.Compile("(a+b)/(a+c)");
            Assert.That(compiled.Execute(new { a, b, c }), Is.EqualTo((a + b) / (a + c)));

            a = 5.4m;
            b = -2.4m;
            c = 7.5m;

            Assert.That(compiled.Execute(new { a, b, c }), Is.EqualTo((a + b) / (a + c)));
        }

        [Test]
        public void Can_Invoke_Two_Expressions_With_Different_Parameters()
        {
            var firstCompiled = _engine.Compile("(a+b)/(a+c)");
            var secondCompiled = _engine.Compile("a * b");

            var a = 6m;
            var b = 3.9m;
            var c = 4.9m;

            Assert.That(firstCompiled.Execute(new { a, b, c }), Is.EqualTo((a + b) / (a + c)));

            a = 7.5m;
            b = 2.2m;
            Assert.That(secondCompiled.Execute(new { a, b }), Is.EqualTo(a * b));
        }
    }
}