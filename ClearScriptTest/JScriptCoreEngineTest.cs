// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.ClearScript.JavaScript;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.ClearScript.Util;
using Microsoft.ClearScript.Windows.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ClearScript.Test
{
    // ReSharper disable once PartialTypeWithSinglePart

    [TestClass]
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Test classes use TestCleanupAttribute for deterministic teardown.")]
    [SuppressMessage("ReSharper", "StringLiteralTypo", Justification = "Typos in test code are acceptable.")]
    public partial class JScriptCoreEngineTest : ClearScriptTest
    {
        #region setup / teardown

        private JScriptEngine engine;

        [TestInitialize]
        public void TestInitialize()
        {
            engine = new JScriptEngine(Windows.WindowsScriptEngineFlags.EnableDebugging, NullSyncInvoker.Instance);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            engine.Dispose();
            BaseTestCleanup();
        }

        #endregion

        #region test methods

        // ReSharper disable InconsistentNaming

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostObject()
        {
            var host = new HostFunctions();
            engine.AddHostObject("host", host);
            Assert.AreSame(host, engine.Evaluate("host"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void JScriptCoreEngine_AddHostObject_Scalar()
        {
            engine.AddHostObject("value", 123);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostObject_Enum()
        {
            const DayOfWeek value = DayOfWeek.Wednesday;
            engine.AddHostObject("value", value);
            Assert.AreEqual(value, engine.Evaluate("value"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostObject_Struct()
        {
            var date = new DateTime(2007, 5, 22, 6, 15, 43);
            engine.AddHostObject("date", date);
            Assert.AreEqual(date, engine.Evaluate("date"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostObject_GlobalMembers()
        {
            var host = new HostFunctions();
            engine.AddHostObject("host", HostItemFlags.GlobalMembers, host);
            Assert.IsInstanceOfType(engine.Evaluate("newObj()"), typeof(PropertyBag));

            engine.AddHostObject("test", HostItemFlags.GlobalMembers, this);
            engine.Execute("TestProperty = newObj()");
            Assert.IsInstanceOfType(TestProperty, typeof(PropertyBag));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        [ExpectedException(typeof(ScriptEngineException))]
        public void JScriptCoreEngine_AddHostObject_DefaultAccess()
        {
            engine.AddHostObject("test", this);
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostObject_PrivateAccess()
        {
            engine.AddHostObject("test", HostItemFlags.PrivateAccess, this);
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddRestrictedHostObject_BaseClass()
        {
            var host = new ExtendedHostFunctions() as HostFunctions;
            engine.AddRestrictedHostObject("host", host);
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj()"), typeof(PropertyBag));
            TestUtil.AssertException<ScriptEngineException>(() => engine.Evaluate("host.type('System.Int32')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddRestrictedHostObject_Interface()
        {
            const double value = 123.45;
            engine.AddRestrictedHostObject("convertible", value as IConvertible);
            engine.AddHostObject("culture", CultureInfo.InvariantCulture);
            Assert.AreEqual(value, engine.Evaluate("convertible.ToDouble(culture)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("Random", typeof(Random));
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(Random)"), typeof(Random));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_GlobalMembers()
        {
            engine.AddHostType("Guid", HostItemFlags.GlobalMembers, typeof(Guid));
            Assert.IsInstanceOfType(engine.Evaluate("NewGuid()"), typeof(Guid));

            engine.AddHostType("Test", HostItemFlags.GlobalMembers, GetType());
            engine.Execute("StaticTestProperty = NewGuid()");
            Assert.IsInstanceOfType(StaticTestProperty, typeof(Guid));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        [ExpectedException(typeof(ScriptEngineException))]
        public void JScriptCoreEngine_AddHostType_DefaultAccess()
        {
            engine.AddHostType("Test", GetType());
            engine.Execute("Test.PrivateStaticMethod()");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_PrivateAccess()
        {
            engine.AddHostType("Test", HostItemFlags.PrivateAccess, GetType());
            engine.Execute("Test.PrivateStaticMethod()");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_Static()
        {
            engine.AddHostType("Enumerable", typeof(Enumerable));
            Assert.IsInstanceOfType(engine.Evaluate("Enumerable.Range(0, 5).ToArray()"), typeof(int[]));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_OpenGeneric()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("List", typeof(List<>));
            engine.AddHostType("Guid", typeof(Guid));
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(List(Guid))"), typeof(List<Guid>));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_ByName()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("Random", "System.Random");
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(Random)"), typeof(Random));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_ByNameWithAssembly()
        {
            engine.AddHostType("Enumerable", "System.Linq.Enumerable", "System.Core");
            Assert.IsInstanceOfType(engine.Evaluate("Enumerable.Range(0, 5).ToArray()"), typeof(int[]));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_ByNameWithTypeArgs()
        {
            engine.AddHostObject("host", new HostFunctions());
            engine.AddHostType("Dictionary", "System.Collections.Generic.Dictionary", typeof(string), typeof(int));
            Assert.IsInstanceOfType(engine.Evaluate("host.newObj(Dictionary)"), typeof(Dictionary<string, int>));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_DefaultName()
        {
            engine.AddHostType(typeof(Random));
            Assert.IsInstanceOfType(engine.Evaluate("new Random()"), typeof(Random));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostType_DefaultNameGeneric()
        {
            engine.AddHostType(typeof(List<int>));
            Assert.IsInstanceOfType(engine.Evaluate("new List()"), typeof(List<int>));

            engine.AddHostType(typeof(Dictionary<,>));
            engine.AddHostType(typeof(int));
            engine.AddHostType(typeof(double));
            Assert.IsInstanceOfType(engine.Evaluate("new Dictionary(Int32, Double, 100)"), typeof(Dictionary<int, double>));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddHostTypes()
        {
            engine.AddHostTypes(typeof(Dictionary<,>), typeof(int), typeof(double));
            Assert.IsInstanceOfType(engine.Evaluate("new Dictionary(Int32, Double, 100)"), typeof(Dictionary<int, double>));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate()
        {
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate("Math.E * Math.PI"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_WithDocumentName()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(documentName, "Math.E * Math.PI"));
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_DiscardDocument()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(documentName, true, "Math.E * Math.PI"));
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_RetainDocument()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(documentName, false, "Math.E * Math.PI"));
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_DocumentInfo_WithDocumentName()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(new DocumentInfo(documentName), "Math.E * Math.PI"));
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_DocumentInfo_WithDocumentUri()
        {
            const string documentName = "DoTheMath";
            var documentUri = new Uri(@"c:\foo\bar\baz\" + documentName);
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(new DocumentInfo(documentUri) { Flags = DocumentFlags.None }, "Math.E * Math.PI"));
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_DocumentInfo_WithDocumentUri_Relative()
        {
            const string documentName = "DoTheMath";
            var documentUri = new Uri(documentName, UriKind.Relative);
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(new DocumentInfo(documentUri) { Flags = DocumentFlags.None }, "Math.E * Math.PI"));
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_DocumentInfo_DiscardDocument()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(new DocumentInfo(documentName) { Flags = DocumentFlags.IsTransient }, "Math.E * Math.PI"));
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Evaluate_DocumentInfo_RetainDocument()
        {
            const string documentName = "DoTheMath";
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate(new DocumentInfo(documentName) { Flags = DocumentFlags.None }, "Math.E * Math.PI"));
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute()
        {
            engine.Execute("epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_WithDocumentName()
        {
            const string documentName = "DoTheMath";
            engine.Execute(documentName, "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_DiscardDocument()
        {
            const string documentName = "DoTheMath";
            engine.Execute(documentName, true, "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_RetainDocument()
        {
            const string documentName = "DoTheMath";
            engine.Execute(documentName, false, "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_DocumentInfo_WithDocumentName()
        {
            const string documentName = "DoTheMath";
            engine.Execute(new DocumentInfo(documentName), "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_DocumentInfo_WithDocumentUri()
        {
            const string documentName = "DoTheMath";
            var documentUri = new Uri(@"c:\foo\bar\baz\" + documentName);
            engine.Execute(new DocumentInfo(documentUri), "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_DocumentInfo_WithDocumentUri_Relative()
        {
            const string documentName = "DoTheMath";
            var documentUri = new Uri(documentName, UriKind.Relative);
            engine.Execute(new DocumentInfo(documentUri), "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_DocumentInfo_DiscardDocument()
        {
            const string documentName = "DoTheMath";
            engine.Execute(new DocumentInfo(documentName) { Flags = DocumentFlags.IsTransient }, "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsFalse(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Execute_DocumentInfo_RetainDocument()
        {
            const string documentName = "DoTheMath";
            engine.Execute(new DocumentInfo(documentName) { Flags = DocumentFlags.None }, "epi = Math.E * Math.PI");
            Assert.AreEqual(Math.E * Math.PI, engine.Script.epi);
            Assert.IsTrue(engine.GetDebugDocumentNames().Any(name => name.StartsWith(documentName, StringComparison.Ordinal)));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ExecuteCommand_EngineConvert()
        {
            Assert.AreEqual("[object Math]", engine.ExecuteCommand("Math"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ExecuteCommand_HostConvert()
        {
            var dateHostItem = HostItem.Wrap(engine, new DateTime(2007, 5, 22, 6, 15, 43));
            engine.AddHostObject("date", dateHostItem);
            Assert.AreEqual(dateHostItem.ToString(), engine.ExecuteCommand("date"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ExecuteCommand_var()
        {
            Assert.AreEqual("[undefined]", engine.ExecuteCommand("var x = 'foo'"));
            Assert.AreEqual("foo", engine.Script.x);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ExecuteCommand_HostVariable()
        {
            engine.Script.host = new HostFunctions();
            Assert.AreEqual("[HostVariable:String]", engine.ExecuteCommand("host.newVar('foo')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Invoke_ScriptFunction()
        {
            engine.Execute("function foo(x) { return x * Math.PI; }");
            Assert.AreEqual(Math.E * Math.PI, engine.Invoke("foo", Math.E));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Invoke_HostDelegate()
        {
            engine.Script.foo = new Func<double, double>(x => x * Math.PI);
            Assert.AreEqual(Math.E * Math.PI, engine.Invoke("foo", Math.E));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Interrupt()
        {
            var checkpoint = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                checkpoint.WaitOne();
                engine.Interrupt();
            });

            engine.AddHostObject("checkpoint", checkpoint);
            TestUtil.AssertException<OperationCanceledException>(() => engine.Execute("checkpoint.Set(); while (true) { var foo = 'hello'; }"));
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate("Math.E * Math.PI"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        [ExpectedException(typeof(ScriptEngineException))]
        public void JScriptCoreEngine_AccessContext_Default()
        {
            engine.AddHostObject("test", this);
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AccessContext_Private()
        {
            engine.AddHostObject("test", this);
            engine.AccessContext = GetType();
            engine.Execute("test.PrivateMethod()");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ContinuationCallback()
        {
            engine.ContinuationCallback = () => false;
            TestUtil.AssertException<OperationCanceledException>(() => engine.Execute("while (true) { var foo = 'hello'; }"));
            engine.ContinuationCallback = null;
            Assert.AreEqual(Math.E * Math.PI, engine.Evaluate("Math.E * Math.PI"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_FileNameExtension()
        {
            Assert.AreEqual("js", engine.FileNameExtension);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property()
        {
            var host = new HostFunctions();
            engine.Script.host = host;
            Assert.AreSame(host, engine.Script.host);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_Scalar()
        {
            const int value = 123;
            engine.Script.value = value;
            Assert.AreEqual(value, engine.Script.value);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_Enum()
        {
            const DayOfWeek value = DayOfWeek.Wednesday;
            engine.Script.value = value;
            Assert.AreEqual(value, engine.Script.value);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_Struct()
        {
            var date = new DateTime(2007, 5, 22, 6, 15, 43);
            engine.Script.date = date;
            Assert.AreEqual(date, engine.Script.date);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Index_ArrayItem()
        {
            const int index = 5;
            engine.Execute("foo = []");

            engine.Script.foo[index] = engine.Script.Math.PI;
            Assert.AreEqual(Math.PI, engine.Script.foo[index]);
            Assert.AreEqual(index + 1, engine.Evaluate("foo.length"));

            engine.Script.foo[index] = engine.Script.Math.E;
            Assert.AreEqual(Math.E, engine.Script.foo[index]);
            Assert.AreEqual(index + 1, engine.Evaluate("foo.length"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Index_Property()
        {
            const string name = "bar";
            engine.Execute("foo = {}");

            engine.Script.foo[name] = engine.Script.Math.PI;
            Assert.AreEqual(Math.PI, engine.Script.foo[name]);
            Assert.AreEqual(Math.PI, engine.Script.foo.bar);

            engine.Script.foo[name] = engine.Script.Math.E;
            Assert.AreEqual(Math.E, engine.Script.foo[name]);
            Assert.AreEqual(Math.E, engine.Script.foo.bar);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Method()
        {
            engine.Execute("function foo(x) { return x * x; }");
            Assert.AreEqual(25, engine.Script.foo(5));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Method_Intrinsic()
        {
            Assert.AreEqual(Math.E * Math.PI, engine.Script.eval("Math.E * Math.PI"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine
                    Dim host As New HostFunctions
                    engine.Script.host = host
                    Assert.AreSame(host, engine.Script.host)
                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_Scalar_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine
                    Dim value = 123
                    engine.Script.value = value
                    Assert.AreEqual(value, engine.Script.value)
                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_Enum_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine
                    Dim value = DayOfWeek.Wednesday
                    engine.Script.value = value
                    Assert.AreEqual(value, engine.Script.value)
                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Property_Struct_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine
                    Dim value As New DateTime(2007, 5, 22, 6, 15, 43)
                    engine.Script.value = value
                    Assert.AreEqual(value, engine.Script.value)
                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Index_ArrayItem_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine

                    Dim index = 5
                    engine.Execute(""foo = []"")

                    engine.Script.foo(index) = engine.Script.Math.PI
                    Assert.AreEqual(Math.PI, engine.Script.foo(index))
                    Assert.AreEqual(index + 1, engine.Evaluate(""foo.length""))

                    engine.Script.foo(index) = engine.Script.Math.E
                    Assert.AreEqual(Math.E, engine.Script.foo(index))
                    Assert.AreEqual(index + 1, engine.Evaluate(""foo.length""))

                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Index_Property_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine

                    Dim name = ""bar""
                    engine.Execute(""foo = {}"")

                    engine.Script.foo(name) = engine.Script.Math.PI
                    Assert.AreEqual(Math.PI, engine.Script.foo(name))
                    Assert.AreEqual(Math.PI, engine.Script.foo.bar)

                    engine.Script.foo(name) = engine.Script.Math.E
                    Assert.AreEqual(Math.E, engine.Script.foo(name))
                    Assert.AreEqual(Math.E, engine.Script.foo.bar)

                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Method_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine
                    engine.Execute(""function foo(x) { return x * x; }"")
                    Assert.AreEqual(25, engine.Script.foo(5))
                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Script_Method_Intrinsic_VB()
        {
            TestUtil.InvokeVBTestSub(@"
                Using engine As New JScriptEngine
                    Assert.AreEqual(Math.E * Math.PI, engine.Script.eval(""Math.E * Math.PI""))
                End Using
            ");
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_CollectGarbage()
        {
            engine.Execute("x = []; for (i = 0; i < 1024 * 1024; i++) { x.push(x); }");
            engine.CollectGarbage(true);
            // can't test JScript GC effectiveness
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib"));
            Assert.IsInstanceOfType(engine.Evaluate("new System.Random()"), typeof(Random));
            Assert.IsInstanceOfType(engine.Evaluate("new System.Random(100)"), typeof(Random));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new_Generic()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib"));
            Assert.IsInstanceOfType(engine.Evaluate("new System.Collections.Generic.Dictionary(System.Int32, System.String)"), typeof(Dictionary<int, string>));
            Assert.IsInstanceOfType(engine.Evaluate("new System.Collections.Generic.Dictionary(System.Int32, System.String, 100)"), typeof(Dictionary<int, string>));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new_GenericNested()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib", "System.Core"));
            engine.AddHostObject("dict", new Dictionary<int, string> { { 12345, "foo" }, { 54321, "bar" } });
            Assert.IsInstanceOfType(engine.Evaluate("vc = new (System.Collections.Generic.Dictionary(System.Int32, System.String).ValueCollection)(dict)"), typeof(Dictionary<int, string>.ValueCollection));
            Assert.IsTrue((bool)engine.Evaluate("vc.SequenceEqual(dict.Values)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new_Scalar()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib"));
            Assert.AreEqual(0, engine.Evaluate("new System.Int32"));
            Assert.AreEqual(0, engine.Evaluate("new System.Int32()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new_Enum()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib"));
            Assert.AreEqual(default(DayOfWeek), engine.Evaluate("new System.DayOfWeek"));
            Assert.AreEqual(default(DayOfWeek), engine.Evaluate("new System.DayOfWeek()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new_Struct()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib"));
            Assert.AreEqual(default(DateTime), engine.Evaluate("new System.DateTime"));
            Assert.AreEqual(default(DateTime), engine.Evaluate("new System.DateTime()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_new_NoMatch()
        {
            engine.AddHostObject("clr", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib"));
            TestUtil.AssertException<MissingMemberException>(() => engine.Execute("new System.Random('a')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_General()
        {
            using (var console = new StringWriter())
            {
                var clr = new HostTypeCollection(type => type != typeof(Console), "mscorlib", "System", "System.Core");
                clr.GetNamespaceNode("System").SetPropertyNoCheck("Console", console);

                engine.AddHostObject("host", new ExtendedHostFunctions());
                engine.AddHostObject("clr", clr);

                engine.Execute(generalScript);
                Assert.AreEqual(MiscHelpers.FormatCode(generalScriptOutput), console.ToString().Replace("\r\n", "\n"));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_SyntaxError()
        {
            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("function foo() { int c; }");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNull(exception.InnerException);
                    Assert.IsTrue(exception.Message.StartsWith("Expected", StringComparison.Ordinal));
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_ThrowNonError()
        {
            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("(function () { throw 123; })()");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNull(exception.InnerException);
                    Assert.IsTrue(exception.ErrorDetails.Contains(" -> throw 123"));
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_ScriptError()
        {
            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("foo = {}; foo();");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNull(exception.InnerException);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_HostException()
        {
            engine.AddHostObject("host", new HostFunctions());

            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Evaluate("host.proc(0)");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNotNull(exception.InnerException);

                    var hostException = exception.InnerException;
                    Assert.IsTrue((hostException is RuntimeBinderException) || (hostException is MissingMethodException));
                    TestUtil.AssertValidException(hostException);
                    Assert.IsNull(hostException.InnerException);

                    Assert.AreEqual(hostException.Message, exception.Message);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_IgnoredHostException()
        {
            engine.AddHostObject("host", new HostFunctions());

            TestUtil.AssertException<ScriptEngineException>(() =>
            {
                try
                {
                    engine.Execute("try { host.newObj(null); } catch(ex) {} foo = {}; foo();");
                }
                catch (ScriptEngineException exception)
                {
                    TestUtil.AssertValidException(engine, exception);
                    Assert.IsNull(exception.InnerException);
                    throw;
                }
            });
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_NestedScriptError()
        {
            using (var innerEngine = new JScriptEngine("inner", Windows.WindowsScriptEngineFlags.EnableDebugging, NullSyncInvoker.Instance))
            {
                engine.AddHostObject("engine", innerEngine);

                TestUtil.AssertException<ScriptEngineException>(() =>
                {
                    try
                    {
                        engine.Execute("engine.Execute('foo = {}; foo();')");
                    }
                    catch (ScriptEngineException exception)
                    {
                        TestUtil.AssertValidException(engine, exception);
                        Assert.IsNotNull(exception.InnerException);

                        var hostException = exception.InnerException;
                        Assert.IsInstanceOfType(hostException, typeof(TargetInvocationException));
                        TestUtil.AssertValidException(hostException);
                        Assert.IsNotNull(hostException.InnerException);

                        var nestedException = hostException.InnerException as ScriptEngineException;
                        Assert.IsNotNull(nestedException);

                        // ReSharper disable once AccessToDisposedClosure
                        TestUtil.AssertValidException(innerEngine, nestedException);

                        Assert.IsNull(nestedException.InnerException);

                        Assert.AreEqual(hostException.GetBaseException().Message, exception.Message);
                        throw;
                    }
                });
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ErrorHandling_NestedHostException()
        {
            using (var innerEngine = new JScriptEngine("inner", Windows.WindowsScriptEngineFlags.EnableDebugging, NullSyncInvoker.Instance))
            {
                innerEngine.AddHostObject("host", new HostFunctions());
                engine.AddHostObject("engine", innerEngine);

                TestUtil.AssertException<ScriptEngineException>(() =>
                {
                    try
                    {
                        engine.Execute("engine.Evaluate('host.proc(0)')");
                    }
                    catch (ScriptEngineException exception)
                    {
                        TestUtil.AssertValidException(engine, exception);
                        Assert.IsNotNull(exception.InnerException);

                        var hostException = exception.InnerException;
                        Assert.IsInstanceOfType(hostException, typeof(TargetInvocationException));
                        TestUtil.AssertValidException(hostException);
                        Assert.IsNotNull(hostException.InnerException);

                        var nestedException = hostException.InnerException as ScriptEngineException;
                        Assert.IsNotNull(nestedException);

                        // ReSharper disable once AccessToDisposedClosure
                        TestUtil.AssertValidException(innerEngine, nestedException);

                        Assert.IsNotNull(nestedException.InnerException);

                        var nestedHostException = nestedException.InnerException;
                        Assert.IsTrue((nestedHostException is RuntimeBinderException) || (nestedHostException is MissingMethodException));
                        TestUtil.AssertValidException(nestedHostException);
                        Assert.IsNull(nestedHostException.InnerException);

                        Assert.AreEqual(nestedHostException.GetBaseException().Message, nestedException.Message);
                        Assert.AreEqual(hostException.GetBaseException().Message, exception.Message);
                        throw;
                    }
                });
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_CreateInstance()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo bar baz qux", engine.Evaluate("new testObject('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_CreateInstance_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("new testObject()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Invoke()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo,bar,baz,qux", engine.Evaluate("testObject('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Invoke_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("testObject()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo-bar-baz-qux", engine.Evaluate("testObject.DynamicMethod('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.DynamicMethod()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_FieldOverride()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo.bar.baz.qux", engine.Evaluate("testObject.SomeField('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_FieldOverride_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.SomeField()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_PropertyOverride()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo:bar:baz:qux", engine.Evaluate("testObject.SomeProperty('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_PropertyOverride_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.SomeProperty()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_DynamicOverload()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("foo;bar;baz;qux", engine.Evaluate("testObject.SomeMethod('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_NonDynamicOverload()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual(Math.PI, engine.Evaluate("testObject.SomeMethod()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_InvokeMethod_NonDynamic()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.AreEqual("Super Bass-O-Matic '76", engine.Evaluate("testObject.ToString()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_StaticType_Field()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.SomeField"), typeof(HostMethod));
            Assert.AreEqual(12345, engine.Evaluate("host.toStaticType(testObject).SomeField"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_StaticType_Property()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.SomeProperty"), typeof(HostMethod));
            Assert.AreEqual("Bogus", engine.Evaluate("host.toStaticType(testObject).SomeProperty"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_StaticType_Method()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.AreEqual("bar+baz+qux", engine.Evaluate("host.toStaticType(testObject).SomeMethod('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_StaticType_Invoke()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            TestUtil.AssertException<NotSupportedException>(() => engine.Evaluate("host.toStaticType(testObject)('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Property()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.foo"), typeof(Undefined));
            Assert.AreEqual(123, engine.Evaluate("testObject.foo = 123"));
            Assert.AreEqual(123, engine.Evaluate("testObject.foo"));
            Assert.IsTrue((bool)engine.Evaluate("delete testObject.foo"));
            Assert.IsInstanceOfType(engine.Evaluate("testObject.foo"), typeof(Undefined));
            TestUtil.AssertException<ScriptEngineException>(() => engine.Evaluate("delete testObject.foo"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Property_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.Zfoo"), typeof(Undefined));
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("testObject.Zfoo = 123"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Property_Invoke()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.foo"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("testObject.foo = function (x) { return x.length; }"), typeof(DynamicObject));
            Assert.AreEqual("floccinaucinihilipilification".Length, engine.Evaluate("testObject.foo('floccinaucinihilipilification')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Property_Invoke_Nested()
        {
            engine.Script.testObject = new DynamicTestObject();
            Assert.IsInstanceOfType(engine.Evaluate("testObject.foo"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("testObject.foo = testObject"), typeof(DynamicTestObject));
            Assert.AreEqual("foo,bar,baz,qux", engine.Evaluate("testObject.foo('foo', 'bar', 'baz', 'qux')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Element()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 1, 2, 3, 'foo')"), typeof(Undefined));
            Assert.AreEqual("bar", engine.Evaluate("host.setElement(testObject, 'bar', 1, 2, 3, 'foo')"));
            Assert.AreEqual("bar", engine.Evaluate("host.getElement(testObject, 1, 2, 3, 'foo')"));
            Assert.IsTrue((bool)engine.Evaluate("host.removeElement(testObject, 1, 2, 3, 'foo')"));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 1, 2, 3, 'foo')"), typeof(Undefined));
            Assert.IsFalse((bool)engine.Evaluate("host.removeElement(testObject, 1, 2, 3, 'foo')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Element_Fail()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 1, 2, 3, Math.PI)"), typeof(Undefined));
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("host.setElement(testObject, 'bar', 1, 2, 3, Math.PI)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Element_Index()
        {
            engine.Script.testObject = new DynamicTestObject { DisableInvocation = true, DisableDynamicMembers = true };
            engine.Script.host = new HostFunctions();

            Assert.IsInstanceOfType(engine.Evaluate("testObject[123]"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 123)"), typeof(Undefined));
            Assert.AreEqual(456, engine.Evaluate("testObject[123] = 456"));
            Assert.AreEqual(456, engine.Evaluate("testObject[123]"));
            Assert.AreEqual(456, engine.Evaluate("host.getElement(testObject, 123)"));
            Assert.IsTrue((bool)engine.Evaluate("delete testObject[123]"));
            Assert.IsInstanceOfType(engine.Evaluate("testObject[123]"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 123)"), typeof(Undefined));

            Assert.IsInstanceOfType(engine.Evaluate("testObject['foo']"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 'foo')"), typeof(Undefined));
            Assert.AreEqual("bar", engine.Evaluate("testObject['foo'] = 'bar'"));
            Assert.AreEqual("bar", engine.Evaluate("testObject['foo']"));
            Assert.AreEqual("bar", engine.Evaluate("host.getElement(testObject, 'foo')"));
            Assert.IsTrue((bool)engine.Evaluate("delete testObject['foo']"));
            Assert.IsInstanceOfType(engine.Evaluate("testObject['foo']"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 'foo')"), typeof(Undefined));

            Assert.IsInstanceOfType(engine.Evaluate("testObject('foo', 'bar', 'baz')"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 'foo', 'bar', 'baz')"), typeof(Undefined));
            Assert.AreEqual("qux", engine.Evaluate("testObject('foo', 'bar', 'baz') = 'qux'"));
            Assert.AreEqual("qux", engine.Evaluate("testObject('foo', 'bar', 'baz')"));
            Assert.AreEqual("qux", engine.Evaluate("host.getElement(testObject, 'foo', 'bar', 'baz')"));
            Assert.IsInstanceOfType(engine.Evaluate("testObject('foo', 'bar', 'baz') = undefined"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("testObject('foo', 'bar', 'baz')"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("host.getElement(testObject, 'foo', 'bar', 'baz')"), typeof(Undefined));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DynamicHostObject_Convert()
        {
            engine.Script.testObject = new DynamicTestObject();
            engine.Script.host = new HostFunctions();
            engine.AddHostType("int_t", typeof(int));
            engine.AddHostType("string_t", typeof(string));
            Assert.AreEqual(98765, engine.Evaluate("host.cast(int_t, testObject)"));
            Assert.AreEqual("Booyakasha!", engine.Evaluate("host.cast(string_t, testObject)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_HostIndexers()
        {
            engine.Script.testObject = new TestObject();

            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item(123)"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item.get(123)"));
            Assert.AreEqual(Math.PI, engine.Evaluate("testObject.Item(123) = Math.PI"));
            Assert.AreEqual(Math.PI, engine.Evaluate("testObject.Item(123)"));
            Assert.AreEqual(Math.E, engine.Evaluate("testObject.Item.set(123, Math.E)"));
            Assert.AreEqual(Math.E, engine.Evaluate("testObject.Item.get(123)"));

            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item('456')"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item.get('456')"));
            Assert.AreEqual(Math.Sqrt(2), engine.Evaluate("testObject.Item('456') = Math.sqrt(2)"));
            Assert.AreEqual(Math.Sqrt(2), engine.Evaluate("testObject.Item('456')"));
            Assert.AreEqual(Math.Sqrt(3), engine.Evaluate("testObject.Item.set('456', Math.sqrt(3))"));
            Assert.AreEqual(Math.Sqrt(3), engine.Evaluate("testObject.Item.get('456')"));

            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item(123, '456', 789.987, -0.12345)"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("testObject.Item.get(123, '456', 789.987, -0.12345)"));
            Assert.AreEqual(Math.Sqrt(5), engine.Evaluate("testObject.Item(123, '456', 789.987, -0.12345) = Math.sqrt(5)"));
            Assert.AreEqual(Math.Sqrt(5), engine.Evaluate("testObject.Item(123, '456', 789.987, -0.12345)"));
            Assert.AreEqual(Math.Sqrt(7), engine.Evaluate("testObject.Item.set(123, '456', 789.987, -0.12345, Math.sqrt(7))"));
            Assert.AreEqual(Math.Sqrt(7), engine.Evaluate("testObject.Item.get(123, '456', 789.987, -0.12345)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_FormatCode()
        {
            try
            {
                engine.Execute("a", "\n\n\n     x = 3.a");
            }
            catch (ScriptEngineException exception)
            {
                Assert.IsTrue(exception.ErrorDetails.Contains("(a:3:11)"));
            }

            engine.FormatCode = true;
            try
            {
                engine.Execute("b", "\n\n\n     x = 3.a");
            }
            catch (ScriptEngineException exception)
            {
                Assert.IsTrue(exception.ErrorDetails.Contains("(b:0:6)"));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_GetStackTrace()
        {
            engine.AddHostObject("qux", new Func<object>(() => engine.GetStackTrace()));
            engine.Execute(@"
                function baz() { return qux(); }
                function bar() { return baz(); }
                function foo() { return bar(); }
            ");

            Assert.AreEqual("    at baz (Script:1:33) -> return qux()\n    at bar (Script:2:33) -> return baz()\n    at foo (Script:3:33) -> return bar()\n    at JScript global code (Script [2] [temp]:0:0) -> foo()", engine.Evaluate("foo()"));
            Assert.AreEqual("    at baz (Script:1:33) -> return qux()\n    at bar (Script:2:33) -> return baz()\n    at foo (Script:3:33) -> return bar()", engine.Script.foo());
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_StandardsMode()
        {
            // ReSharper disable once AccessToDisposedClosure
            TestUtil.AssertException<ScriptEngineException>(() => engine.Evaluate("JSON"));

            engine.Dispose();
            engine = new JScriptEngine(Windows.WindowsScriptEngineFlags.EnableDebugging | Windows.WindowsScriptEngineFlags.EnableStandardsMode, NullSyncInvoker.Instance);

            Assert.AreEqual("{\"foo\":123,\"bar\":456.789}", engine.Evaluate("JSON.stringify({ foo: 123, bar: 456.789 })"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_MarshalNullAsDispatch()
        {
            engine.Script.func = new Func<object>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));

            engine.Dispose();
            engine = new JScriptEngine(Windows.WindowsScriptEngineFlags.EnableDebugging | Windows.WindowsScriptEngineFlags.MarshalNullAsDispatch, NullSyncInvoker.Instance);

            engine.Script.func = new Func<object>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<string>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<bool?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<char?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<sbyte?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<byte?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<short?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<ushort?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<int?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<uint?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<long?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<ulong?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<float?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<double?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<decimal?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<Random>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
            engine.Script.func = new Func<DayOfWeek?>(() => null);
            Assert.IsTrue((bool)engine.Evaluate("func() === null"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_MarshalDecimalAsCurrency()
        {
            engine.Script.func = new Func<object>(() => 123.456M);
            Assert.AreEqual("number", engine.Evaluate("typeof(func())"));
            Assert.AreEqual(123.456 + 5, engine.Evaluate("func() + 5"));

            engine.Dispose();
            engine = new JScriptEngine(Windows.WindowsScriptEngineFlags.EnableDebugging | Windows.WindowsScriptEngineFlags.MarshalDecimalAsCurrency, NullSyncInvoker.Instance);

            engine.Script.func = new Func<object>(() => 123.456M);
            Assert.AreEqual("number", engine.Evaluate("typeof(func())"));
            Assert.AreEqual(123.456 + 5, engine.Evaluate("func() + 5"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_MarshalArraysByValue()
        {
            var foo = new[] { DayOfWeek.Saturday, DayOfWeek.Friday, DayOfWeek.Thursday };

            engine.Script.foo = foo;
            Assert.AreSame(foo, engine.Evaluate("foo"));

            engine.Dispose();
            engine = new JScriptEngine(Windows.WindowsScriptEngineFlags.EnableDebugging | Windows.WindowsScriptEngineFlags.MarshalArraysByValue, NullSyncInvoker.Instance);

            engine.Script.foo = foo;
            Assert.AreNotSame(foo, engine.Evaluate("foo"));
            engine.Execute("foo = new VBArray(foo)");

            Assert.AreEqual(foo.GetUpperBound(0), engine.Evaluate("foo.ubound(1)"));
            for (var index = 0; index < foo.Length; index++)
            {
                Assert.AreEqual(foo[index], engine.Evaluate("foo.getItem(" + index + ")"));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_MarshalArraysByValue_invokeMethod()
        {
            var args = new[] { Math.PI, Math.E };

            engine.Script.args = args;
            engine.Execute("function foo(a, b) { return a * b; }");
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("EngineInternal.invokeMethod(null, foo, args)"));

            engine.Dispose();
            engine = new JScriptEngine(Windows.WindowsScriptEngineFlags.EnableDebugging | Windows.WindowsScriptEngineFlags.MarshalArraysByValue, NullSyncInvoker.Instance);

            engine.Script.args = args;
            engine.Execute("function foo(a, b) { return a * b; }");
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("EngineInternal.invokeMethod(null, foo, args)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMObject_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                fso = host.newComObj('Scripting.FileSystemObject');
                drives = fso.Drives;
                e = drives.GetEnumerator();
                while (e.MoveNext()) {
                    list.Add(e.Current.Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMObject_FileSystemObject_Iteration()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                fso = host.newComObj('Scripting.FileSystemObject');
                drives = fso.Drives;
                for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {
                    list.Add(e.item().Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMObject_FileSystemObject_Iteration_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var list = new ArrayList();

                engine.Script.host = new ExtendedHostFunctions();
                engine.Script.list = list;

                var drivesName = IsNetFramework ? "drives" : "Drives";
                engine.Execute($@"
                    fso = host.newComObj('Scripting.FileSystemObject');
                    drives = fso.{drivesName};
                    for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {{
                        list.add(e.item().path);
                    }}
                ");

                var drives = DriveInfo.GetDrives();
                Assert.AreEqual(drives.Length, list.Count);
                Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMObject_FileSystemObject_TypeLibEnums()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.Execute(@"
                fso = host.newComObj('Scripting.FileSystemObject');
                enums = host.typeLibEnums(fso);
            ");

            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.BinaryCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.BinaryCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.DatabaseCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.DatabaseCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.TextCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.TextCompare)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForAppending), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForAppending)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForReading), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForReading)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForWriting), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForWriting)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateFalse), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateFalse)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateMixed), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateMixed)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateTrue), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateTrue)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateUseDefault), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateUseDefault)"));

            engine.Execute(@"
                function writeFile(contents) {
                    var name = fso.GetTempName();
                    var path = fso.GetSpecialFolder(enums.Scripting.SpecialFolderConst.TemporaryFolder).Path + '\\' + name;
                    var stream = fso.OpenTextFile(path, enums.Scripting.IOMode.ForWriting, true, enums.Scripting.Tristate.TristateTrue);
                    stream.Write(contents);
                    stream.Close();
                    return path;
                }
            ");

            var contents = Guid.NewGuid().ToString();
            var path = engine.Script.writeFile(contents);
            Assert.IsTrue(new FileInfo(path).Length >= (contents.Length * 2));
            Assert.AreEqual(contents, File.ReadAllText(path));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMObject_Dictionary()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.Execute(@"
                dict = host.newComObj('Scripting.Dictionary');
                dict.Add('foo', Math.PI);
                dict.Add('bar', Math.E);
                dict.Add('baz', 'abc');
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item.set('foo', 'pushkin');
                dict.Item.set('bar', 'gogol');
                dict.Item.set('baz', Math.PI * Math.E);
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item('foo') = 987.654;
                dict.Item('bar') = 321;
                dict.Item('baz') = 'halloween';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Key.set('foo', 'qux');
                dict.Key.set('bar', Math.PI);
                dict.Key.set('baz', Math.E);
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('qux')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('qux')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item(Math.PI)"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get(Math.PI)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(Math.E)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(Math.E)"));

            engine.Execute(@"
                dict.Key('qux') = 'foo';
                dict.Key(Math.PI) = 'bar';
                dict.Key(Math.E) = 'baz';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMType_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                FSO = host.comType('Scripting.FileSystemObject');
                fso = host.newObj(FSO);
                drives = fso.Drives;
                e = drives.GetEnumerator();
                while (e.MoveNext()) {
                    list.Add(e.Current.Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMType_FileSystemObject_Iteration()
        {
            var list = new ArrayList();

            engine.Script.host = new ExtendedHostFunctions();
            engine.Script.list = list;
            engine.Execute(@"
                FSO = host.comType('Scripting.FileSystemObject');
                fso = host.newObj(FSO);
                drives = fso.Drives;
                for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {
                    list.Add(e.item().Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMType_FileSystemObject_Iteration_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var list = new ArrayList();

                engine.Script.host = new ExtendedHostFunctions();
                engine.Script.list = list;

                var drivesName = IsNetFramework ? "drives" : "Drives";
                engine.Execute($@"
                    FSO = host.comType('Scripting.FileSystemObject');
                    fso = host.newObj(FSO);
                    drives = fso.{drivesName};
                    for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {{
                        list.add(e.item().path);
                    }}
                ");

                var drives = DriveInfo.GetDrives();
                Assert.AreEqual(drives.Length, list.Count);
                Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMType_FileSystemObject_TypeLibEnums()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.Execute(@"
                FSO = host.comType('Scripting.FileSystemObject');
                fso = host.newObj(FSO);
                enums = host.typeLibEnums(fso);
            ");

            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.BinaryCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.BinaryCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.DatabaseCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.DatabaseCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.TextCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.TextCompare)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForAppending), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForAppending)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForReading), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForReading)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForWriting), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForWriting)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateFalse), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateFalse)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateMixed), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateMixed)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateTrue), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateTrue)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateUseDefault), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateUseDefault)"));

            engine.Execute(@"
                function writeFile(contents) {
                    var name = fso.GetTempName();
                    var path = fso.GetSpecialFolder(enums.Scripting.SpecialFolderConst.TemporaryFolder).Path + '\\' + name;
                    var stream = fso.OpenTextFile(path, enums.Scripting.IOMode.ForWriting, true, enums.Scripting.Tristate.TristateTrue);
                    stream.Write(contents);
                    stream.Close();
                    return path;
                }
            ");

            var contents = Guid.NewGuid().ToString();
            var path = engine.Script.writeFile(contents);
            Assert.IsTrue(new FileInfo(path).Length >= (contents.Length * 2));
            Assert.AreEqual(contents, File.ReadAllText(path));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_COMType_Dictionary()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.Execute(@"
                Dict = host.comType('Scripting.Dictionary');
                dict = host.newObj(Dict);
                dict.Add('foo', Math.PI);
                dict.Add('bar', Math.E);
                dict.Add('baz', 'abc');
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item.set('foo', 'pushkin');
                dict.Item.set('bar', 'gogol');
                dict.Item.set('baz', Math.PI * Math.E);
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item('foo') = 987.654;
                dict.Item('bar') = 321;
                dict.Item('baz') = 'halloween';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Key.set('foo', 'qux');
                dict.Key.set('bar', Math.PI);
                dict.Key.set('baz', Math.E);
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('qux')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('qux')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item(Math.PI)"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get(Math.PI)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(Math.E)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(Math.E)"));

            engine.Execute(@"
                dict.Key('qux') = 'foo';
                dict.Key(Math.PI) = 'bar';
                dict.Key(Math.E) = 'baz';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMObject_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.list = list;
            engine.AddCOMObject("fso", "Scripting.FileSystemObject");
            engine.Execute(@"
                drives = fso.Drives;
                e = drives.GetEnumerator();
                while (e.MoveNext()) {
                    list.Add(e.Current.Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));

            Assert.AreEqual("[HostObject:IFileSystem3]", engine.ExecuteCommand("fso"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMObject_FileSystemObject_Iteration()
        {
            var list = new ArrayList();

            engine.Script.list = list;
            engine.AddCOMObject("fso", "Scripting.FileSystemObject");
            engine.Execute(@"
                drives = fso.Drives;
                for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {
                    list.Add(e.item().Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMObject_FileSystemObject_Iteration_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var list = new ArrayList();

                engine.Script.list = list;
                engine.AddCOMObject("fso", "Scripting.FileSystemObject");

                var drivesName = IsNetFramework ? "drives" : "Drives";
                engine.Execute($@"
                    drives = fso.{drivesName};
                    for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {{
                        list.add(e.item().path);
                    }}
                ");

                var drives = DriveInfo.GetDrives();
                Assert.AreEqual(drives.Length, list.Count);
                Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMObject_FileSystemObject_TypeLibEnums()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.AddCOMObject("fso", "Scripting.FileSystemObject");
            engine.Execute(@"
                enums = host.typeLibEnums(fso);
            ");

            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.BinaryCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.BinaryCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.DatabaseCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.DatabaseCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.TextCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.TextCompare)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForAppending), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForAppending)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForReading), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForReading)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForWriting), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForWriting)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateFalse), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateFalse)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateMixed), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateMixed)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateTrue), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateTrue)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateUseDefault), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateUseDefault)"));

            engine.Execute(@"
                function writeFile(contents) {
                    var name = fso.GetTempName();
                    var path = fso.GetSpecialFolder(enums.Scripting.SpecialFolderConst.TemporaryFolder).Path + '\\' + name;
                    var stream = fso.OpenTextFile(path, enums.Scripting.IOMode.ForWriting, true, enums.Scripting.Tristate.TristateTrue);
                    stream.Write(contents);
                    stream.Close();
                    return path;
                }
            ");

            var contents = Guid.NewGuid().ToString();
            var path = engine.Script.writeFile(contents);
            Assert.IsTrue(new FileInfo(path).Length >= (contents.Length * 2));
            Assert.AreEqual(contents, File.ReadAllText(path));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMObject_FileSystemObject_DirectAccess()
        {
            var list = new ArrayList();

            engine.Script.list = list;
            engine.AddCOMObject("fso", HostItemFlags.DirectAccess, "Scripting.FileSystemObject");
            engine.Execute(@"
                drives = fso.Drives;
                for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {
                    list.Add(e.item().Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));

            Assert.AreEqual("System.__ComObject", engine.ExecuteCommand("fso"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMObject_Dictionary()
        {
            engine.AddCOMObject("dict", new Guid("{ee09b103-97e0-11cf-978f-00a02463e06f}"));
            engine.Execute(@"
                dict.Add('foo', Math.PI);
                dict.Add('bar', Math.E);
                dict.Add('baz', 'abc');
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item.set('foo', 'pushkin');
                dict.Item.set('bar', 'gogol');
                dict.Item.set('baz', Math.PI * Math.E);
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item('foo') = 987.654;
                dict.Item('bar') = 321;
                dict.Item('baz') = 'halloween';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Key.set('foo', 'qux');
                dict.Key.set('bar', Math.PI);
                dict.Key.set('baz', Math.E);
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('qux')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('qux')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item(Math.PI)"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get(Math.PI)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(Math.E)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(Math.E)"));

            engine.Execute(@"
                dict.Key('qux') = 'foo';
                dict.Key(Math.PI) = 'bar';
                dict.Key(Math.E) = 'baz';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMType_FileSystemObject()
        {
            var list = new ArrayList();

            engine.Script.list = list;
            engine.AddCOMType("FSO", "Scripting.FileSystemObject");
            engine.Execute(@"
                fso = new FSO();
                drives = fso.Drives;
                e = drives.GetEnumerator();
                while (e.MoveNext()) {
                    list.Add(e.Current.Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMType_FileSystemObject_Iteration()
        {
            var list = new ArrayList();

            engine.Script.list = list;
            engine.AddCOMType("FSO", "Scripting.FileSystemObject");
            engine.Execute(@"
                fso = new FSO();
                drives = fso.Drives;
                for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {
                    list.Add(e.item().Path);
                }
            ");

            var drives = DriveInfo.GetDrives();
            Assert.AreEqual(drives.Length, list.Count);
            Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMType_FileSystemObject_Iteration_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var list = new ArrayList();

                engine.Script.list = list;
                engine.AddCOMType("FSO", "Scripting.FileSystemObject");

                var drivesName = IsNetFramework ? "drives" : "Drives";
                engine.Execute($@"
                    fso = new FSO();
                    drives = fso.{drivesName};
                    for (e = new Enumerator(drives); !e.atEnd(); e.moveNext()) {{
                        list.add(e.item().path);
                    }}
                ");

                var drives = DriveInfo.GetDrives();
                Assert.AreEqual(drives.Length, list.Count);
                Assert.IsTrue(drives.Select(drive => drive.Name.Substring(0, 2)).SequenceEqual(list.ToArray()));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMType_FileSystemObject_TypeLibEnums()
        {
            engine.Script.host = new ExtendedHostFunctions();
            engine.AddCOMType("FSO", "Scripting.FileSystemObject");
            engine.Execute(@"
                fso = new FSO();
                enums = host.typeLibEnums(fso);
            ");

            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.BinaryCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.BinaryCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.DatabaseCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.DatabaseCompare)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.CompareMethod.TextCompare), engine.Evaluate("host.toInt32(enums.Scripting.CompareMethod.TextCompare)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForAppending), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForAppending)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForReading), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForReading)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.IOMode.ForWriting), engine.Evaluate("host.toInt32(enums.Scripting.IOMode.ForWriting)"));

            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateFalse), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateFalse)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateMixed), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateMixed)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateTrue), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateTrue)"));
            Assert.AreEqual(Convert.ToInt32(Scripting.Tristate.TristateUseDefault), engine.Evaluate("host.toInt32(enums.Scripting.Tristate.TristateUseDefault)"));

            engine.Execute(@"
                function writeFile(contents) {
                    var name = fso.GetTempName();
                    var path = fso.GetSpecialFolder(enums.Scripting.SpecialFolderConst.TemporaryFolder).Path + '\\' + name;
                    var stream = fso.OpenTextFile(path, enums.Scripting.IOMode.ForWriting, true, enums.Scripting.Tristate.TristateTrue);
                    stream.Write(contents);
                    stream.Close();
                    return path;
                }
            ");

            var contents = Guid.NewGuid().ToString();
            var path = engine.Script.writeFile(contents);
            Assert.IsTrue(new FileInfo(path).Length >= (contents.Length * 2));
            Assert.AreEqual(contents, File.ReadAllText(path));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_AddCOMType_Dictionary()
        {
            engine.AddCOMType("Dict", new Guid("{ee09b103-97e0-11cf-978f-00a02463e06f}"));
            engine.Execute(@"
                dict = new Dict();
                dict.Add('foo', Math.PI);
                dict.Add('bar', Math.E);
                dict.Add('baz', 'abc');
            ");

            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(Math.PI, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(Math.E, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("abc", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item.set('foo', 'pushkin');
                dict.Item.set('bar', 'gogol');
                dict.Item.set('baz', Math.PI * Math.E);
            ");

            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual("pushkin", engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual("gogol", engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual(Math.PI * Math.E, engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Item('foo') = 987.654;
                dict.Item('bar') = 321;
                dict.Item('baz') = 'halloween';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));

            engine.Execute(@"
                dict.Key.set('foo', 'qux');
                dict.Key.set('bar', Math.PI);
                dict.Key.set('baz', Math.E);
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('qux')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('qux')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item(Math.PI)"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get(Math.PI)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item(Math.E)"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get(Math.E)"));

            engine.Execute(@"
                dict.Key('qux') = 'foo';
                dict.Key(Math.PI) = 'bar';
                dict.Key(Math.E) = 'baz';
            ");

            Assert.AreEqual(987.654, engine.Evaluate("dict.Item('foo')"));
            Assert.AreEqual(987.654, engine.Evaluate("dict.Item.get('foo')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item('bar')"));
            Assert.AreEqual(321, engine.Evaluate("dict.Item.get('bar')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item('baz')"));
            Assert.AreEqual("halloween", engine.Evaluate("dict.Item.get('baz')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EnableAutoHostVariables()
        {
            const string pre = "123";
            var value = "foo";
            const int post = 456;

            engine.Execute("function foo(a, x, b) { var y = x; x = a + 'bar' + b; return y; }");
            Assert.AreEqual("foo", engine.Script.foo(pre, ref value, post));
            Assert.AreEqual("foo", value);  // JavaScript doesn't support output parameters

            engine.EnableAutoHostVariables = true;
            engine.Execute("function foo(a, x, b) { var y = x.value; x.value = a + 'bar' + b; return y; }");
            Assert.AreEqual("foo", engine.Script.foo(pre, ref value, post));
            Assert.AreEqual("123bar456", value);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EnableAutoHostVariables_Delegate()
        {
            const string pre = "123";
            var value = "foo";
            const int post = 456;

            engine.Execute("function foo(a, x, b) { var y = x; x = a + 'bar' + b; return y; }");
            var del = DelegateFactory.CreateDelegate<TestDelegate>(engine, engine.Evaluate("foo"));
            Assert.AreEqual("foo", del(pre, ref value, post));
            Assert.AreEqual("foo", value);  // JavaScript doesn't support output parameters

            engine.EnableAutoHostVariables = true;
            engine.Execute("function foo(a, x, b) { var y = x.value; x.value = a + 'bar' + b; return y; }");
            del = DelegateFactory.CreateDelegate<TestDelegate>(engine, engine.Evaluate("foo"));
            Assert.AreEqual("foo", del(pre, ref value, post));
            Assert.AreEqual("123bar456", value);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Current()
        {
            using (var innerEngine = new JScriptEngine(NullSyncInvoker.Instance))
            {
                engine.Script.test = new Action(() =>
                {
                    // ReSharper disable AccessToDisposedClosure

                    innerEngine.Script.test = new Action(() => Assert.AreSame(innerEngine, ScriptEngine.Current));
                    Assert.AreSame(engine, ScriptEngine.Current);
                    innerEngine.Execute("test()");
                    innerEngine.Script.test();
                    Assert.AreSame(engine, ScriptEngine.Current);

                    // ReSharper restore AccessToDisposedClosure
                });

                Assert.IsNull(ScriptEngine.Current);
                engine.Execute("test()");
                engine.Script.test();
                Assert.IsNull(ScriptEngine.Current);
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EnableNullResultWrapping()
        {
            var testValue = new[] { 1, 2, 3, 4, 5 };
            engine.Script.host = new HostFunctions();
            engine.Script.foo = new NullResultWrappingTestObject<int[]>(testValue);

            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("foo.Value === null")));
            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.Value)")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("foo.NullValue === null")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.NullValue)")));
            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("foo.WrappedNullValue === null")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.WrappedNullValue)")));

            Assert.AreSame(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            TestUtil.AssertException<RuntimeBinderException, AmbiguousMatchException>(() => engine.Evaluate("foo.Method(foo.NullValue)"));

            engine.EnableNullResultWrapping = true;
            Assert.AreSame(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.NullValue)"));

            engine.EnableNullResultWrapping = false;
            Assert.AreSame(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            TestUtil.AssertException<RuntimeBinderException, AmbiguousMatchException>(() => engine.Evaluate("foo.Method(foo.NullValue)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EnableNullResultWrapping_String()
        {
            const string testValue = "bar";
            engine.Script.host = new HostFunctions();
            engine.Script.foo = new NullResultWrappingTestObject<string>(testValue);

            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("foo.Value === null")));
            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.Value)")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("foo.NullValue === null")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.NullValue)")));
            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("foo.WrappedNullValue === null")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.WrappedNullValue)")));

            Assert.AreEqual(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            TestUtil.AssertException<RuntimeBinderException, AmbiguousMatchException>(() => engine.Evaluate("foo.Method(foo.NullValue)"));

            engine.EnableNullResultWrapping = true;
            Assert.AreEqual(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.NullValue)"));

            engine.EnableNullResultWrapping = false;
            Assert.AreEqual(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            TestUtil.AssertException<RuntimeBinderException, AmbiguousMatchException>(() => engine.Evaluate("foo.Method(foo.NullValue)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EnableNullResultWrapping_Nullable()
        {
            int? testValue = 12345;
            engine.Script.host = new HostFunctions();
            engine.Script.foo = new NullResultWrappingTestObject<int?>(testValue);

            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("foo.Value === null")));
            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.Value)")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("foo.NullValue === null")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.NullValue)")));
            Assert.IsFalse(Convert.ToBoolean(engine.Evaluate("foo.WrappedNullValue === null")));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("host.isNull(foo.WrappedNullValue)")));

            Assert.AreEqual(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            TestUtil.AssertException<RuntimeBinderException, AmbiguousMatchException>(() => engine.Evaluate("foo.Method(foo.NullValue)"));

            engine.EnableNullResultWrapping = true;
            Assert.AreEqual(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.NullValue)"));

            engine.EnableNullResultWrapping = false;
            Assert.AreEqual(testValue, engine.Evaluate("foo.Method(foo.Value)"));
            Assert.IsNull(engine.Evaluate("foo.Method(foo.WrappedNullValue)"));
            TestUtil.AssertException<RuntimeBinderException, AmbiguousMatchException>(() => engine.Evaluate("foo.Method(foo.NullValue)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DefaultProperty()
        {
            engine.Script.foo = new DefaultPropertyTestObject();
            engine.AddHostType("DayOfWeek", typeof(DayOfWeek));

            engine.Execute("foo('abc') = 123");
            Assert.AreEqual(123, engine.Evaluate("foo('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Item('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Item.get('abc')"));
            Assert.IsNull(engine.Evaluate("foo('def')"));

            engine.Execute("foo(DayOfWeek.Thursday) = 456");
            Assert.AreEqual(456, engine.Evaluate("foo(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Item(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Item.get(DayOfWeek.Thursday)"));
            Assert.IsNull(engine.Evaluate("foo(DayOfWeek.Friday)"));

            engine.Execute("foo.Item('def') = 987");
            Assert.AreEqual(987, engine.Evaluate("foo('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Item('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Item.get('def')"));
            Assert.IsNull(engine.Evaluate("foo('ghi')"));

            engine.Execute("foo.Item(DayOfWeek.Friday) = 654");
            Assert.AreEqual(654, engine.Evaluate("foo(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Item(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Item.get(DayOfWeek.Friday)"));
            Assert.IsNull(engine.Evaluate("foo(DayOfWeek.Saturday)"));

            engine.Execute("foo.Item.set('ghi', 321)");
            Assert.AreEqual(321, engine.Evaluate("foo('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Item('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Item.get('ghi')"));
            Assert.IsNull(engine.Evaluate("foo('jkl')"));

            engine.Execute("foo.Item.set(DayOfWeek.Saturday, -123)");
            Assert.AreEqual(-123, engine.Evaluate("foo(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Item(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Item.get(DayOfWeek.Saturday)"));
            Assert.IsNull(engine.Evaluate("foo(DayOfWeek.Sunday)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DefaultProperty_FieldTunneling()
        {
            engine.Script.foo = new DefaultPropertyTestContainer();
            engine.AddHostType("DayOfWeek", typeof(DayOfWeek));

            engine.Execute("foo.Field('abc') = 123");
            Assert.AreEqual(123, engine.Evaluate("foo.Field('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Field.Item('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Field.Item.get('abc')"));
            Assert.IsNull(engine.Evaluate("foo.Field('def')"));

            engine.Execute("foo.Field(DayOfWeek.Thursday) = 456");
            Assert.AreEqual(456, engine.Evaluate("foo.Field(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Field.Item(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Field.Item.get(DayOfWeek.Thursday)"));
            Assert.IsNull(engine.Evaluate("foo.Field(DayOfWeek.Friday)"));

            engine.Execute("foo.Field.Item('def') = 987");
            Assert.AreEqual(987, engine.Evaluate("foo.Field('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Field.Item('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Field.Item.get('def')"));
            Assert.IsNull(engine.Evaluate("foo.Field('ghi')"));

            engine.Execute("foo.Field.Item(DayOfWeek.Friday) = 654");
            Assert.AreEqual(654, engine.Evaluate("foo.Field(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Field.Item(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Field.Item.get(DayOfWeek.Friday)"));
            Assert.IsNull(engine.Evaluate("foo.Field(DayOfWeek.Saturday)"));

            engine.Execute("foo.Field.Item.set('ghi', 321)");
            Assert.AreEqual(321, engine.Evaluate("foo.Field('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Field.Item('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Field.Item.get('ghi')"));
            Assert.IsNull(engine.Evaluate("foo.Field('jkl')"));

            engine.Execute("foo.Field.Item.set(DayOfWeek.Saturday, -123)");
            Assert.AreEqual(-123, engine.Evaluate("foo.Field(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Field.Item(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Field.Item.get(DayOfWeek.Saturday)"));
            Assert.IsNull(engine.Evaluate("foo.Field(DayOfWeek.Sunday)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DefaultProperty_PropertyTunneling()
        {
            engine.Script.foo = new DefaultPropertyTestContainer();
            engine.AddHostType("DayOfWeek", typeof(DayOfWeek));

            engine.Execute("foo.Property('abc') = 123");
            Assert.AreEqual(123, engine.Evaluate("foo.Property('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Property.Item('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Property.Item.get('abc')"));
            Assert.IsNull(engine.Evaluate("foo.Property('def')"));

            engine.Execute("foo.Property(DayOfWeek.Thursday) = 456");
            Assert.AreEqual(456, engine.Evaluate("foo.Property(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Property.Item(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Property.Item.get(DayOfWeek.Thursday)"));
            Assert.IsNull(engine.Evaluate("foo.Property(DayOfWeek.Friday)"));

            engine.Execute("foo.Property.Item('def') = 987");
            Assert.AreEqual(987, engine.Evaluate("foo.Property('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Property.Item('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Property.Item.get('def')"));
            Assert.IsNull(engine.Evaluate("foo.Property('ghi')"));

            engine.Execute("foo.Property.Item(DayOfWeek.Friday) = 654");
            Assert.AreEqual(654, engine.Evaluate("foo.Property(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Property.Item(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Property.Item.get(DayOfWeek.Friday)"));
            Assert.IsNull(engine.Evaluate("foo.Property(DayOfWeek.Saturday)"));

            engine.Execute("foo.Property.Item.set('ghi', 321)");
            Assert.AreEqual(321, engine.Evaluate("foo.Property('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Property.Item('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Property.Item.get('ghi')"));
            Assert.IsNull(engine.Evaluate("foo.Property('jkl')"));

            engine.Execute("foo.Property.Item.set(DayOfWeek.Saturday, -123)");
            Assert.AreEqual(-123, engine.Evaluate("foo.Property(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Property.Item(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Property.Item.get(DayOfWeek.Saturday)"));
            Assert.IsNull(engine.Evaluate("foo.Property(DayOfWeek.Sunday)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DefaultProperty_MethodTunneling()
        {
            engine.Script.foo = new DefaultPropertyTestContainer();
            engine.AddHostType("DayOfWeek", typeof(DayOfWeek));

            engine.Execute("foo.Method()('abc') = 123");
            Assert.AreEqual(123, engine.Evaluate("foo.Method()('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Method().Item('abc')"));
            Assert.AreEqual(123, engine.Evaluate("foo.Method().Item.get('abc')"));
            Assert.IsNull(engine.Evaluate("foo.Method()('def')"));

            engine.Execute("foo.Method()(DayOfWeek.Thursday) = 456");
            Assert.AreEqual(456, engine.Evaluate("foo.Method()(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Method().Item(DayOfWeek.Thursday)"));
            Assert.AreEqual(456, engine.Evaluate("foo.Method().Item.get(DayOfWeek.Thursday)"));
            Assert.IsNull(engine.Evaluate("foo.Method()(DayOfWeek.Friday)"));

            engine.Execute("foo.Method().Item('def') = 987");
            Assert.AreEqual(987, engine.Evaluate("foo.Method()('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Method().Item('def')"));
            Assert.AreEqual(987, engine.Evaluate("foo.Method().Item.get('def')"));
            Assert.IsNull(engine.Evaluate("foo.Method()('ghi')"));

            engine.Execute("foo.Method().Item(DayOfWeek.Friday) = 654");
            Assert.AreEqual(654, engine.Evaluate("foo.Method()(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Method().Item(DayOfWeek.Friday)"));
            Assert.AreEqual(654, engine.Evaluate("foo.Method().Item.get(DayOfWeek.Friday)"));
            Assert.IsNull(engine.Evaluate("foo.Method()(DayOfWeek.Saturday)"));

            engine.Execute("foo.Method().Item.set('ghi', 321)");
            Assert.AreEqual(321, engine.Evaluate("foo.Method()('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Method().Item('ghi')"));
            Assert.AreEqual(321, engine.Evaluate("foo.Method().Item.get('ghi')"));
            Assert.IsNull(engine.Evaluate("foo.Method()('jkl')"));

            engine.Execute("foo.Method().Item.set(DayOfWeek.Saturday, -123)");
            Assert.AreEqual(-123, engine.Evaluate("foo.Method()(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Method().Item(DayOfWeek.Saturday)"));
            Assert.AreEqual(-123, engine.Evaluate("foo.Method().Item.get(DayOfWeek.Saturday)"));
            Assert.IsNull(engine.Evaluate("foo.Method()(DayOfWeek.Sunday)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DefaultProperty_Indexer()
        {
            engine.Script.dict = new Dictionary<string, object> { { "abc", 123 }, { "def", 456 }, { "ghi", 789 } };
            engine.Execute("item = dict.Item");

            Assert.AreEqual(123, engine.Evaluate("item('abc')"));
            Assert.AreEqual(456, engine.Evaluate("item('def')"));
            Assert.AreEqual(789, engine.Evaluate("item('ghi')"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("item('jkl')"));

            engine.Execute("item('abc') = 'foo'");
            Assert.AreEqual("foo", engine.Evaluate("item('abc')"));
            Assert.AreEqual(456, engine.Evaluate("item('def')"));
            Assert.AreEqual(789, engine.Evaluate("item('ghi')"));
            TestUtil.AssertException<KeyNotFoundException>(() => engine.Evaluate("item('jkl')"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_PropertyAndMethodWithSameName()
        {
            engine.AddHostObject("lib", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib", "System", "System.Core"));

            engine.Script.dict = new Dictionary<string, object> { { "abc", 123 }, { "def", 456 }, { "ghi", 789 } };
            Assert.AreEqual(3, engine.Evaluate("dict.Count"));
            Assert.AreEqual(3, engine.Evaluate("dict.Count()"));

            engine.Script.listDict = new ListDictionary { { "abc", 123 }, { "def", 456 }, { "ghi", 789 } };
            Assert.AreEqual(3, engine.Evaluate("listDict.Count"));
            TestUtil.AssertMethodBindException(() => engine.Evaluate("listDict.Count()"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration()
        {
            var array = Enumerable.Range(0, 10).ToArray();
            engine.Execute(@"
                function sum(array) {
                    var result = 0;
                    for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                        result += e.item();
                    }
                    return result;
                }
            ");
            Assert.AreEqual(array.Aggregate((current, next) => current + next), engine.Script.sum(array));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var array = Enumerable.Range(0, 10).ToArray();
                engine.Execute(@"
                    function sum(array) {
                        var result = 0;
                        for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                            result += e.item();
                        }
                        return result;
                    }
                ");
                Assert.AreEqual(array.Aggregate((current, next) => current + next), engine.Script.sum(array));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_Generic()
        {
            var array = Enumerable.Range(0, 10).Select(value => (IConvertible)value).ToArray();
            engine.Script.culture = CultureInfo.InvariantCulture;
            engine.Execute(@"
                function sum(array) {
                    var result = 0;
                    for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                        result += e.item().ToInt32(culture);
                    }
                    return result;
                }
            ");
            Assert.AreEqual(array.Aggregate((current, next) => Convert.ToInt32(current) + Convert.ToInt32(next)), engine.Script.sum(array));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_Generic_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var array = Enumerable.Range(0, 10).Select(value => (IConvertible)value).ToArray();
                engine.Script.culture = CultureInfo.InvariantCulture;
                engine.Execute(@"
                    function sum(array) {
                        var result = 0;
                        for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                            result += e.item().toInt32(culture);
                        }
                        return result;
                    }
                ");
                Assert.AreEqual(array.Aggregate((current, next) => Convert.ToInt32(current) + Convert.ToInt32(next)), engine.Script.sum(array));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_NonGeneric()
        {
            var array = Enumerable.Range(0, 10).ToArray();
            engine.Execute(@"
                function sum(array) {
                    var result = 0;
                    for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                        result += e.item();
                    }
                    return result;
                }
            ");
            Assert.AreEqual(array.Aggregate((current, next) => current + next), engine.Script.sum(HostObject.Wrap(array, typeof(IEnumerable))));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_NonGeneric_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                var array = Enumerable.Range(0, 10).ToArray();
                engine.Execute(@"
                    function sum(array) {
                        var result = 0;
                        for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                            result += e.item();
                        }
                        return result;
                    }
                ");
                Assert.AreEqual(array.Aggregate((current, next) => current + next), engine.Script.sum(HostObject.Wrap(array, typeof(IEnumerable))));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_NonEnumerable()
        {
            engine.Execute(@"
                function sum(array) {
                    var result = 0;
                    for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                        result += e.item();
                    }
                    return result;
                }
            ");
            TestUtil.AssertException<ScriptEngineException>(() => engine.Script.sum(DayOfWeek.Monday));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Iteration_NonEnumerable_GlobalRenaming()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();

                engine.Execute(@"
                    function sum(array) {
                        var result = 0;
                        for (var e = new Enumerator(array); !e.atEnd(); e.moveNext()) {
                            result += e.item();
                        }
                        return result;
                    }
                ");
                TestUtil.AssertException<ScriptEngineException>(() => engine.Script.sum(DayOfWeek.Monday));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ScriptObject()
        {
            var obj = engine.Evaluate("({})") as ScriptObject;
            Assert.IsNotNull(obj);
            Assert.AreSame(engine, obj.Engine);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ScriptObject_IDictionary()
        {
            // ReSharper disable UsageOfDefaultStructEquality

            var pairs = new List<KeyValuePair<string, object>>
            {
                new("123", 987),
                new("456", 654.321),
                new("abc", 123),
                new("def", 456.789),
                new("ghi", "foo"),
                new("jkl", engine.Evaluate("({ bar: 'baz' })"))
            };

            var dict = (IDictionary<string, object>)engine.Evaluate("dict = {}");

            pairs.ForEach(pair => dict.Add(pair));
            Assert.IsTrue(dict.SequenceEqual(pairs));

            var index = 0;
            foreach (var pair in dict)
            {
                Assert.AreEqual(pairs[index++], pair);
            }

            index = 0;
            foreach (var pair in (IEnumerable)dict)
            {
                Assert.AreEqual(pairs[index++], pair);
            }

            dict.Clear();
            Assert.AreEqual(0, dict.Count);

            pairs.ForEach(pair => dict.Add(pair.Key, pair.Value));
            Assert.IsTrue(dict.SequenceEqual(pairs));

            Assert.IsTrue(pairs.All(pair => dict.Contains(pair)));
            Assert.IsTrue(pairs.All(pair => dict.ContainsKey(pair.Key)));

            var testPairs = new KeyValuePair<string, object>[pairs.Count + 3];
            dict.CopyTo(testPairs, 3);
            Assert.IsTrue(testPairs.Skip(3).SequenceEqual(pairs));

            Assert.IsTrue(pairs.All(pair => dict.Remove(pair)));
            Assert.AreEqual(0, dict.Count);

            pairs.ForEach(pair => dict.Add(pair.Key, pair.Value));
            Assert.IsTrue(dict.SequenceEqual(pairs));

            Assert.IsTrue(pairs.All(pair => dict.Remove(pair.Key)));
            Assert.AreEqual(0, dict.Count);

            pairs.ForEach(pair => dict.Add(pair.Key, pair.Value));
            Assert.IsTrue(dict.SequenceEqual(pairs));

            Assert.IsTrue(pairs.All(pair => dict.TryGetValue(pair.Key, out var value) && Equals(value, pair.Value)));
            Assert.IsTrue(pairs.All(pair => Equals(dict[pair.Key], pair.Value)));

            Assert.IsTrue(pairs.Select(pair => pair.Key).SequenceEqual(dict.Keys));
            Assert.IsTrue(pairs.Select(pair => pair.Value).SequenceEqual(dict.Values));

            Assert.IsFalse(dict.TryGetValue("qux", out _));
            TestUtil.AssertException<KeyNotFoundException>(() => Assert.IsTrue(dict["qux"] is Undefined));

            engine.Execute("dict[789] = Math.PI");
            Assert.IsTrue(dict.TryGetValue("789", out var pi) && Equals(pi, Math.PI));
            Assert.IsFalse(pairs.SequenceEqual(dict));

            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("delete dict[789]")));
            Assert.IsTrue(pairs.SequenceEqual(dict));

            // ReSharper restore UsageOfDefaultStructEquality
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ArrayInvocability()
        {
            engine.Script.foo = Enumerable.Range(123, 5).ToArray();
            Assert.AreEqual(124, engine.Evaluate("foo(1)"));
            Assert.AreEqual(456, engine.Evaluate("foo(1) = 456"));

            engine.Script.foo = new IConvertible[] { "bar" };
            Assert.AreEqual("bar", engine.Evaluate("foo(0)"));
            Assert.AreEqual("baz", engine.Evaluate("foo(0) = 'baz'"));

            engine.Script.bar = new List<string>();
            TestUtil.AssertMethodBindException(() => engine.Execute("bar.Add(foo(0))"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_PropertyBagInvocability()
        {
            engine.Script.lib = new HostTypeCollection("mscorlib", "System", "System.Core");
            Assert.IsInstanceOfType(engine.Evaluate("lib('System')"), typeof(PropertyBag));
            Assert.IsInstanceOfType(engine.Evaluate("lib.System('Collections')"), typeof(PropertyBag));
            Assert.IsInstanceOfType(engine.Evaluate("lib('Bogus')"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("lib.System('Heinous')"), typeof(Undefined));
            TestUtil.AssertException<UnauthorizedAccessException>(() => engine.Execute("lib('Bogus') = 123"));
            TestUtil.AssertException<UnauthorizedAccessException>(() => engine.Execute("lib.System('Heinous') = 456"));

            engine.Script.foo = new PropertyBag { { "Null", null } };
            Assert.IsNull(engine.Evaluate("foo.Null"));
            TestUtil.AssertException<InvalidOperationException>(() => engine.Evaluate("foo.Null(123)"));
            engine.Execute("foo(null) = 123");
            Assert.AreEqual(123, Convert.ToInt32(engine.Evaluate("foo(null)")));
            engine.Execute("foo(undefined) = 456");
            Assert.AreEqual(456, Convert.ToInt32(engine.Evaluate("foo(undefined)")));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EnforceAnonymousTypeAccess()
        {
            engine.Script.foo = new { bar = 123, baz = "qux" };
            Assert.AreEqual(123, engine.Evaluate("foo.bar"));
            Assert.AreEqual("qux", engine.Evaluate("foo.baz"));

            engine.EnforceAnonymousTypeAccess = true;
            Assert.IsInstanceOfType(engine.Evaluate("foo.bar"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("foo.baz"), typeof(Undefined));

            engine.AccessContext = GetType();
            Assert.AreEqual(123, engine.Evaluate("foo.bar"));
            Assert.AreEqual("qux", engine.Evaluate("foo.baz"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_UnderlyingObject()
        {
            engine.Execute("function Foo() {}; bar = new Foo();");

            var bar = (Windows.IWindowsScriptObject)(((ScriptObject)engine.Script)["bar"]);
            var underlyingObject = bar.GetUnderlyingObject();

            Assert.AreEqual("JScriptTypeInfo", TestUtil.GetCOMObjectTypeName(underlyingObject));

            bar.Dispose();
            Assert.AreEqual(0, Marshal.ReleaseComObject(underlyingObject));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ScriptObjectMembers()
        {
            engine.Execute(@"
                function Foo() {
                    this.Qux = function (x) { this.Bar = x; };
                    this.Xuq = function () { return this.Baz; }
                }
            ");

            var foo = (ScriptObject)engine.Evaluate("new Foo");

            foo.SetProperty("Bar", 123);
            Assert.AreEqual(123, foo.GetProperty("Bar"));

            foo["Baz"] = "abc";
            Assert.AreEqual("abc", foo.GetProperty("Baz"));

            foo.InvokeMethod("Qux", DayOfWeek.Wednesday);
            Assert.AreEqual(DayOfWeek.Wednesday, foo.GetProperty("Bar"));

            foo["Baz"] = BindingFlags.ExactBinding;
            Assert.AreEqual(BindingFlags.ExactBinding, foo.InvokeMethod("Xuq"));

            foo[1] = new HostFunctions();
            Assert.IsInstanceOfType(foo[1], typeof(HostFunctions));
            Assert.IsInstanceOfType(foo[2], typeof(Undefined));

            var names = foo.PropertyNames.ToArray();
            Assert.AreEqual(4, names.Length);
            Assert.IsTrue(names.Contains("Bar"));
            Assert.IsTrue(names.Contains("Baz"));
            Assert.IsTrue(names.Contains("Qux"));
            Assert.IsTrue(names.Contains("Xuq"));

            var indices = foo.PropertyIndices.ToArray();
            Assert.AreEqual(1, indices.Length);
            Assert.IsTrue(indices.Contains(1));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_Nothing()
        {
            engine.Script.foo = new Func<object>(() => Windows.Nothing.Value);
            Assert.IsTrue((bool)engine.Evaluate("foo() == undefined"));
            Assert.IsFalse((bool)engine.Evaluate("foo() === undefined"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ExecuteDocument_Script()
        {
            engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;

            using (var console = new StringWriter())
            {
                var clr = new HostTypeCollection(type => type != typeof(Console), "mscorlib", "System", "System.Core");
                clr.GetNamespaceNode("System").SetPropertyNoCheck("Console", console);

                engine.AddHostObject("host", new ExtendedHostFunctions());
                engine.AddHostObject("clr", clr);

                engine.ExecuteDocument("JavaScript/General.js");
                Assert.AreEqual(MiscHelpers.FormatCode(generalScriptOutput), console.ToString().Replace("\r\n", "\n"));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EvaluateDocument_Script()
        {
            engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;

            using (var console = new StringWriter())
            {
                var clr = new HostTypeCollection(type => type != typeof(Console), "mscorlib", "System", "System.Core");
                clr.GetNamespaceNode("System").SetPropertyNoCheck("Console", console);

                engine.AddHostObject("host", new ExtendedHostFunctions());
                engine.AddHostObject("clr", clr);

                Assert.AreEqual((int)Math.Round(Math.Sin(Math.PI) * 1000e16), engine.EvaluateDocument("JavaScript/General.js"));
                Assert.AreEqual(MiscHelpers.FormatCode(generalScriptOutput), console.ToString().Replace("\r\n", "\n"));
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_EvaluateDocument_Module_CommonJS()
        {
            engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
            Assert.AreEqual(25 * 25, engine.EvaluateDocument("JavaScript/LegacyCommonJS/Module", ModuleCategory.CommonJS));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DocumentSettings_EnforceRelativePrefix()
        {
            engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading | DocumentAccessFlags.EnforceRelativePrefix;
            TestUtil.AssertException<FileNotFoundException>(() => engine.EvaluateDocument("JavaScript/LegacyCommonJS/Module", ModuleCategory.CommonJS));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_UndefinedImportValue()
        {
            Assert.IsNull(engine.Evaluate("null"));
            Assert.IsInstanceOfType(engine.Evaluate("undefined"), typeof(Undefined));

            engine.UndefinedImportValue = null;
            Assert.IsNull(engine.Evaluate("null"));
            Assert.IsNull(engine.Evaluate("undefined"));

            engine.UndefinedImportValue = 123;
            Assert.IsNull(engine.Evaluate("null"));
            Assert.AreEqual(123, engine.Evaluate("undefined"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_NullImportValue()
        {
            Assert.IsNull(engine.Evaluate("null"));
            Assert.IsInstanceOfType(engine.Evaluate("undefined"), typeof(Undefined));

            engine.NullImportValue = Undefined.Value;
            Assert.IsInstanceOfType(engine.Evaluate("null"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("undefined"), typeof(Undefined));

            engine.NullImportValue = 123;
            Assert.AreEqual(123, engine.Evaluate("null"));
            Assert.IsInstanceOfType(engine.Evaluate("undefined"), typeof(Undefined));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_NullExportValue()
        {
            engine.Script.foo = new Func<object>(() => null);
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("foo() === null")));

            engine.NullExportValue = Undefined.Value;
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("foo() === undefined")));

            engine.NullExportValue = null;
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("foo() === null")));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_VoidResultValue()
        {
            engine.Script.foo = new Action(() => {});
            Assert.IsInstanceOfType(engine.Evaluate("foo()"), typeof(VoidResult));

            engine.VoidResultValue = 123;
            Assert.AreEqual(123, engine.Evaluate("foo()"));

            engine.VoidResultValue = Undefined.Value;
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("typeof(foo()) === 'undefined'")));

            engine.VoidResultValue = VoidResult.Value;
            Assert.IsInstanceOfType(engine.Evaluate("foo()"), typeof(VoidResult));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ExposeStaticMembersOnHostObjects()
        {
            engine.Script.utf8 = Encoding.UTF8;
            Assert.AreEqual("utf-8", engine.Evaluate("utf8.WebName"));
            Assert.IsInstanceOfType(engine.Evaluate("utf8.ASCII"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("utf8.ReferenceEquals"), typeof(Undefined));

            engine.ExposeHostObjectStaticMembers = true;
            Assert.AreEqual("utf-8", engine.Evaluate("utf8.WebName"));
            Assert.IsInstanceOfType(engine.Evaluate("utf8.ASCII"), typeof(Encoding));
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("utf8.ReferenceEquals(null, null)")));

            engine.ExposeHostObjectStaticMembers = false;
            Assert.AreEqual("utf-8", engine.Evaluate("utf8.WebName"));
            Assert.IsInstanceOfType(engine.Evaluate("utf8.ASCII"), typeof(Undefined));
            Assert.IsInstanceOfType(engine.Evaluate("utf8.ReferenceEquals"), typeof(Undefined));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DirectAccess_Normal()
        {
            engine.Script.test = new DirectAccessTestObject();
            engine.AddHostObject("daTest", HostItemFlags.DirectAccess, engine.Script.test);

            Assert.AreEqual("[HostObject:JScriptCoreEngineTest.DirectAccessTestObject]", engine.ExecuteCommand("test"));
            Assert.AreEqual("[HostObject:JScriptCoreEngineTest.DirectAccessTestObject]", engine.ExecuteCommand("daTest"));

            Assert.AreEqual("123 456.789 qux", engine.Evaluate("test.Format('{0} {1} {2}', 123, 456.789, 'qux')"));
            Assert.AreEqual("123 456.789 qux", engine.Evaluate("daTest.Format('{0} {1} {2}', 123, 456.789, 'qux')"));

            Assert.AreEqual(0, engine.Evaluate("test.Bogus(123.456)"));
            Assert.AreEqual(0, engine.Evaluate("daTest.Bogus(123.456)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DirectAccess_ComVisible()
        {
            engine.Script.test = new ComVisibleTestObject();
            engine.AddHostObject("daTest", HostItemFlags.DirectAccess, engine.Script.test);

            Assert.AreEqual("[HostObject:JScriptCoreEngineTest.ComVisibleTestObject]", engine.ExecuteCommand("test"));
            Assert.AreNotEqual("[HostObject:JScriptCoreEngineTest.ComVisibleTestObject]", engine.ExecuteCommand("daTest"));

            Assert.AreEqual("123 456.789 qux", engine.Evaluate("test.Format('{0} {1} {2}', 123, 456.789, 'qux')"));
            Assert.AreEqual("123 456.789 qux", engine.Evaluate("daTest.Format('{0} {1} {2}', 123, 456.789, 'qux')"));

            Assert.AreEqual(0, engine.Evaluate("test.Bogus(123.456)"));
            TestUtil.AssertException<ScriptEngineException>(() => engine.Evaluate("daTest.Bogus(123.456)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_isPromise()
        {
            Assert.IsInstanceOfType(engine.Script.Promise, typeof(Undefined));

            engine.Execute("function Promise() { this.foo = 123; } value = new Promise();");
            Assert.AreEqual(123, engine.Script.value.foo);
            Assert.IsTrue(Convert.ToBoolean(engine.Evaluate("value instanceof Promise")));

            Assert.IsFalse(engine.Script.EngineInternal.isPromise(engine.Script.value));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DisableExtensionMethods()
        {
            engine.AddHostType("Str", typeof(string));
            engine.AddHostType(typeof(Enumerable));
            engine.AddHostType("Filter", typeof(Func<int, bool>));
            engine.Script.array = Enumerable.Range(0, 10).ToArray();

            Assert.IsFalse(engine.DisableExtensionMethods);
            Assert.IsFalse(engine.Evaluate("array.Where") is Undefined);
            Assert.AreEqual("1,3,5,7,9", engine.Evaluate("Str.Join(',', array.Where(new Filter(function (n) { return (n & 1) === 1; })))"));

            engine.DisableExtensionMethods = true;
            Assert.IsTrue(engine.DisableExtensionMethods);
            Assert.IsTrue(engine.Evaluate("array.Where") is Undefined);
            TestUtil.AssertException<MissingMemberException>(() => engine.Evaluate("Str.Join(',', array.Where(new Filter(function (n) { return (n & 1) === 1; })))"));

            engine.DisableExtensionMethods = false;
            Assert.IsFalse(engine.DisableExtensionMethods);
            Assert.IsFalse(engine.Evaluate("array.Where") is Undefined);
            Assert.AreEqual("1,3,5,7,9", engine.Evaluate("Str.Join(',', array.Where(new Filter(function (n) { return (n & 1) === 1; })))"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_DisableFloatNarrowing()
        {
            engine.AddHostType("StringT", typeof(string));
            Assert.AreEqual("123,456.80", engine.Evaluate("StringT.Format('{0:###,###.00}', 123456.75)"));
            engine.DisableFloatNarrowing = true;
            Assert.AreEqual("123,456.75", engine.Evaluate("StringT.Format('{0:###,###.00}', 123456.75)"));
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_CustomAttributeLoader()
        {
            using (Scope.Create(() => HostSettings.CustomAttributeLoader, loader => HostSettings.CustomAttributeLoader = loader))
            {
                HostSettings.CustomAttributeLoader = new CamelCaseAttributeLoader();
                TestCamelCaseMemberBinding();
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_CustomAttributeLoader_Private()
        {
            using (var otherEngine = new JScriptEngine(NullSyncInvoker.Instance))
            {
                engine.CustomAttributeLoader = new CamelCaseAttributeLoader();
                TestCamelCaseMemberBinding();

                using (Scope.Create(() => engine, originalEngine => engine = originalEngine))
                {
                    engine = otherEngine;
                    TestUtil.AssertException<InvalidCastException>(TestCamelCaseMemberBinding);
                }
            }
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_ScriptObjectIdentity()
        {
            var list = new List<object>();
            engine.Script.list = list;

            engine.Execute(@"
                obj = {};
                list.Add(obj);
                func = function () {};
                list.Add(func);
            ");

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(engine.Script.obj, list[0]);
            Assert.AreEqual(engine.Script.func, list[1]);

            Assert.AreEqual(true, engine.Evaluate("list.Remove(obj)"));
            Assert.AreEqual(false, engine.Evaluate("list.Remove(obj)"));

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(engine.Script.func, list[0]);

            Assert.AreEqual(true, engine.Evaluate("list.Remove(func)"));
            Assert.AreEqual(false, engine.Evaluate("list.Remove(func)"));

            Assert.AreEqual(0, list.Count);
        }

        [TestMethod, TestCategory("JScriptCoreEngine")]
        public void JScriptCoreEngine_JavaScriptObjectKindAndFlags()
        {
            (JavaScriptObjectKind, JavaScriptObjectFlags) Inspect(string expression)
            {
                var obj = (IJavaScriptObject)engine.Evaluate($"({expression}).valueOf()");
                return (obj.Kind, obj.Flags);
            }

            Assert.AreEqual((JavaScriptObjectKind.Unknown, JavaScriptObjectFlags.None), Inspect("{}"));
            Assert.AreEqual((JavaScriptObjectKind.Unknown, JavaScriptObjectFlags.None), Inspect("function () {}"));
            Assert.AreEqual((JavaScriptObjectKind.Unknown, JavaScriptObjectFlags.None), Inspect("[]"));
        }

        // ReSharper restore InconsistentNaming

        #endregion

        #region miscellaneous

        private const string generalScript =
        @"
            System = clr.System;

            TestObject = host.type('Microsoft.ClearScript.Test.GeneralTestObject', 'ClearScriptTest');
            tlist = host.newObj(System.Collections.Generic.List(TestObject));
            tlist.Add(host.newObj(TestObject, 'Eóin', 20));
            tlist.Add(host.newObj(TestObject, 'Shane', 16));
            tlist.Add(host.newObj(TestObject, 'Cillian', 8));
            tlist.Add(host.newObj(TestObject, 'Sasha', 6));
            tlist.Add(host.newObj(TestObject, 'Brian', 3));

            olist = host.newObj(System.Collections.Generic.List(System.Object));
            olist.Add({ name: 'Brian', age: 3 });
            olist.Add({ name: 'Sasha', age: 6 });
            olist.Add({ name: 'Cillian', age: 8 });
            olist.Add({ name: 'Shane', age: 16 });
            olist.Add({ name: 'Eóin', age: 20 });

            dict = host.newObj(System.Collections.Generic.Dictionary(System.String, System.String));
            dict.Add('foo', 'bar');
            dict.Add('baz', 'qux');
            value = host.newVar(System.String);
            result = dict.TryGetValue('foo', value.out);

            bag = host.newObj();
            bag.method = function (x) { System.Console.WriteLine(x * x); };
            bag.proc = host.del(System.Action(System.Object), bag.method);

            expando = host.newObj(System.Dynamic.ExpandoObject);
            expandoCollection = host.cast(System.Collections.Generic.ICollection(System.Collections.Generic.KeyValuePair(System.String, System.Object)), expando);

            function onChange(s, e) {
                System.Console.WriteLine('Property changed: {0}; new value: {1}', e.PropertyName, s[e.PropertyName]);
            };
            function onStaticChange(s, e) {
                System.Console.WriteLine('Property changed: {0}; new value: {1} (static event)', e.PropertyName, e.PropertyValue);
            };
            eventCookie = tlist.Item(0).Change.connect(onChange);
            staticEventCookie = TestObject.StaticChange.connect(onStaticChange);
            tlist.Item(0).Name = 'Jerry';
            tlist.Item(1).Name = 'Ellis';
            tlist.Item(0).Name = 'Eóin';
            tlist.Item(1).Name = 'Shane';
            eventCookie.disconnect();
            staticEventCookie.disconnect();
            tlist.Item(0).Name = 'Jerry';
            tlist.Item(1).Name = 'Ellis';
            tlist.Item(0).Name = 'Eóin';
            tlist.Item(1).Name = 'Shane';
        ";

        private const string generalScriptOutput =
        @"
            Property changed: Name; new value: Jerry
            Property changed: Name; new value: Jerry (static event)
            Property changed: Name; new value: Ellis (static event)
            Property changed: Name; new value: Eóin
            Property changed: Name; new value: Eóin (static event)
            Property changed: Name; new value: Shane (static event)
        ";

        public object TestProperty { get; set; }

        public static object StaticTestProperty { get; set; }

        private void TestCamelCaseMemberBinding()
        {
            var random = MiscHelpers.CreateSeededRandom();
            var makeIntArray = new Func<int[]>(() => Enumerable.Range(0, random.Next(5, 25)).Select(_ => random.Next(int.MinValue, int.MaxValue)).ToArray());
            var makeShort = new Func<short>(() => Convert.ToInt16(random.Next(short.MinValue, short.MaxValue)));
            var makeEnum = new Func<TestEnum>(() => (TestEnum)random.Next(0, 5));
            var makeTimeSpan = new Func<TimeSpan>(() => TimeSpan.FromMilliseconds(makeShort()));

            var testObject = new TestObject
            {
                BaseField = makeIntArray(),
                BaseScalarField = makeShort(),
                BaseEnumField = makeEnum(),
                BaseStructField = makeTimeSpan(),

                BaseProperty = makeIntArray(),
                BaseScalarProperty = makeShort(),
                BaseEnumProperty = makeEnum(),
                BaseStructProperty = makeTimeSpan(),

                BaseInterfaceProperty = makeIntArray(),
                BaseInterfaceScalarProperty = makeShort(),
                BaseInterfaceEnumProperty = makeEnum(),
                BaseInterfaceStructProperty = makeTimeSpan(),

                Field = new[] { 0, 9, 1, 8, 2, 7, 3, 6, 4, 5 },
                ScalarField = makeShort(),
                EnumField = makeEnum(),
                StructField = makeTimeSpan(),

                Property = makeIntArray(),
                ScalarProperty = makeShort(),
                EnumProperty = makeEnum(),
                StructProperty = makeTimeSpan(),

                InterfaceProperty = makeIntArray(),
                InterfaceScalarProperty = makeShort(),
                InterfaceEnumProperty = makeEnum(),
                InterfaceStructProperty = makeTimeSpan(),
            };

            var explicitBaseTestInterface = (IExplicitBaseTestInterface)testObject;
            explicitBaseTestInterface.ExplicitBaseInterfaceProperty = makeIntArray();
            explicitBaseTestInterface.ExplicitBaseInterfaceScalarProperty = makeShort();
            explicitBaseTestInterface.ExplicitBaseInterfaceEnumProperty = makeEnum();
            explicitBaseTestInterface.ExplicitBaseInterfaceStructProperty = makeTimeSpan();

            var explicitTestInterface = (IExplicitTestInterface)testObject;
            explicitTestInterface.ExplicitInterfaceProperty = makeIntArray();
            explicitTestInterface.ExplicitInterfaceScalarProperty = makeShort();
            explicitTestInterface.ExplicitInterfaceEnumProperty = makeEnum();
            explicitTestInterface.ExplicitInterfaceStructProperty = makeTimeSpan();

            engine.AddHostType(typeof(TestEnum));
            engine.Script.testObject = testObject;
            engine.Script.testBaseInterface = testObject.ToRestrictedHostObject<IBaseTestInterface>(engine);
            engine.Script.testInterface = testObject.ToRestrictedHostObject<ITestInterface>(engine);
            engine.Script.explicitBaseTestInterface = testObject.ToRestrictedHostObject<IExplicitBaseTestInterface>(engine);
            engine.Script.explicitTestInterface = testObject.ToRestrictedHostObject<IExplicitTestInterface>(engine);

            Assert.IsTrue(testObject.BaseField.SequenceEqual((int[])engine.Evaluate("testObject.baseField")));
            Assert.AreEqual(testObject.BaseScalarField, Convert.ToInt16(engine.Evaluate("testObject.baseScalarField")));
            Assert.AreEqual(testObject.BaseEnumField, engine.Evaluate("testObject.baseEnumField"));
            Assert.AreEqual(testObject.BaseStructField, engine.Evaluate("testObject.baseStructField"));

            Assert.IsTrue(testObject.BaseProperty.SequenceEqual((int[])engine.Evaluate("testObject.baseProperty")));
            Assert.AreEqual(testObject.BaseScalarProperty, Convert.ToInt16(engine.Evaluate("testObject.baseScalarProperty")));
            Assert.AreEqual(testObject.BaseEnumProperty, engine.Evaluate("testObject.baseEnumProperty"));
            Assert.AreEqual(testObject.BaseStructProperty, engine.Evaluate("testObject.baseStructProperty"));
            Assert.AreEqual(testObject.BaseReadOnlyProperty, Convert.ToByte(engine.Evaluate("testObject.baseReadOnlyProperty")));

            engine.Execute("var connection = testObject.baseEvent.connect(function (sender, args) { sender.baseScalarProperty = args.arg; });");
            var arg = makeShort();
            testObject.BaseFireEvent(arg);
            Assert.AreEqual(arg, testObject.BaseScalarProperty);
            engine.Execute("connection.disconnect();");
            testObject.BaseFireEvent(makeShort());
            Assert.AreEqual(arg, testObject.BaseScalarProperty);

            Assert.AreEqual(testObject.BaseMethod("foo", 4), engine.Evaluate("testObject.baseMethod('foo', 4)"));
            Assert.AreEqual(testObject.BaseMethod("foo", 4, TestEnum.Second), engine.Evaluate("testObject.baseMethod('foo', 4, TestEnum.second)"));
            Assert.AreEqual(testObject.BaseMethod<TestEnum>(4), engine.Evaluate("testObject.baseMethod(TestEnum, 4)"));
            Assert.AreEqual(testObject.BaseBindTestMethod(Math.PI), engine.Evaluate("testObject.baseBindTestMethod(Math.PI)"));

            Assert.IsTrue(testObject.BaseInterfaceProperty.SequenceEqual((int[])engine.Evaluate("testObject.baseInterfaceProperty")));
            Assert.AreEqual(testObject.BaseInterfaceScalarProperty, Convert.ToInt16(engine.Evaluate("testObject.baseInterfaceScalarProperty")));
            Assert.AreEqual(testObject.BaseInterfaceEnumProperty, engine.Evaluate("testObject.baseInterfaceEnumProperty"));
            Assert.AreEqual(testObject.BaseInterfaceStructProperty, engine.Evaluate("testObject.baseInterfaceStructProperty"));
            Assert.AreEqual(testObject.BaseInterfaceReadOnlyProperty, Convert.ToByte(engine.Evaluate("testObject.baseInterfaceReadOnlyProperty")));

            engine.Execute("var connection = testObject.baseInterfaceEvent.connect(function (sender, args) { sender.baseInterfaceScalarProperty = args.arg; });");
            arg = makeShort();
            testObject.BaseInterfaceFireEvent(arg);
            Assert.AreEqual(arg, testObject.BaseInterfaceScalarProperty);
            engine.Execute("connection.disconnect();");
            testObject.BaseInterfaceFireEvent(makeShort());
            Assert.AreEqual(arg, testObject.BaseInterfaceScalarProperty);

            Assert.AreEqual(testObject.BaseInterfaceMethod("foo", 4), engine.Evaluate("testObject.baseInterfaceMethod('foo', 4)"));
            Assert.AreEqual(testObject.BaseInterfaceMethod("foo", 4, TestEnum.Second), engine.Evaluate("testObject.baseInterfaceMethod('foo', 4, TestEnum.second)"));
            Assert.AreEqual(testObject.BaseInterfaceMethod<TestEnum>(4), engine.Evaluate("testObject.baseInterfaceMethod(TestEnum, 4)"));
            Assert.AreEqual(testObject.BaseInterfaceBindTestMethod(Math.PI), engine.Evaluate("testObject.baseInterfaceBindTestMethod(Math.PI)"));

            Assert.IsTrue(testObject.Field.SequenceEqual((int[])engine.Evaluate("testObject.field")));
            Assert.AreEqual(testObject.ScalarField, Convert.ToInt16(engine.Evaluate("testObject.scalarField")));
            Assert.AreEqual(testObject.EnumField, engine.Evaluate("testObject.enumField"));
            Assert.AreEqual(testObject.StructField, engine.Evaluate("testObject.structField"));

            Assert.IsTrue(testObject.Property.SequenceEqual((int[])engine.Evaluate("testObject.property")));
            Assert.AreEqual(testObject.ScalarProperty, Convert.ToInt16(engine.Evaluate("testObject.scalarProperty")));
            Assert.AreEqual(testObject.EnumProperty, engine.Evaluate("testObject.enumProperty"));
            Assert.AreEqual(testObject.StructProperty, engine.Evaluate("testObject.structProperty"));
            Assert.AreEqual(testObject.ReadOnlyProperty, Convert.ToByte(engine.Evaluate("testObject.readOnlyProperty")));

            engine.Execute("var connection = testObject.event.connect(function (sender, args) { sender.scalarProperty = args.arg; });");
            arg = makeShort();
            testObject.FireEvent(arg);
            Assert.AreEqual(arg, testObject.ScalarProperty);
            engine.Execute("connection.disconnect();");
            testObject.FireEvent(makeShort());
            Assert.AreEqual(arg, testObject.ScalarProperty);

            Assert.AreEqual(testObject.Method("foo", 4), engine.Evaluate("testObject.method('foo', 4)"));
            Assert.AreEqual(testObject.Method("foo", 4, TestEnum.Second), engine.Evaluate("testObject.method('foo', 4, TestEnum.second)"));
            Assert.AreEqual(testObject.Method<TestEnum>(4), engine.Evaluate("testObject.method(TestEnum, 4)"));
            Assert.AreEqual(testObject.BindTestMethod(Math.PI), engine.Evaluate("testObject.bindTestMethod(Math.PI)"));

            Assert.IsTrue(testObject.InterfaceProperty.SequenceEqual((int[])engine.Evaluate("testObject.interfaceProperty")));
            Assert.AreEqual(testObject.InterfaceScalarProperty, Convert.ToInt16(engine.Evaluate("testObject.interfaceScalarProperty")));
            Assert.AreEqual(testObject.InterfaceEnumProperty, engine.Evaluate("testObject.interfaceEnumProperty"));
            Assert.AreEqual(testObject.InterfaceStructProperty, engine.Evaluate("testObject.interfaceStructProperty"));
            Assert.AreEqual(testObject.InterfaceReadOnlyProperty, Convert.ToByte(engine.Evaluate("testObject.interfaceReadOnlyProperty")));

            engine.Execute("var connection = testObject.interfaceEvent.connect(function (sender, args) { sender.interfaceScalarProperty = args.arg; });");
            arg = makeShort();
            testObject.InterfaceFireEvent(arg);
            Assert.AreEqual(arg, testObject.InterfaceScalarProperty);
            engine.Execute("connection.disconnect();");
            testObject.InterfaceFireEvent(makeShort());
            Assert.AreEqual(arg, testObject.InterfaceScalarProperty);

            Assert.AreEqual(testObject.InterfaceMethod("foo", 4), engine.Evaluate("testObject.interfaceMethod('foo', 4)"));
            Assert.AreEqual(testObject.InterfaceMethod("foo", 4, TestEnum.Second), engine.Evaluate("testObject.interfaceMethod('foo', 4, TestEnum.second)"));
            Assert.AreEqual(testObject.InterfaceMethod<TestEnum>(4), engine.Evaluate("testObject.interfaceMethod(TestEnum, 4)"));
            Assert.AreEqual(testObject.InterfaceBindTestMethod(Math.PI), engine.Evaluate("testObject.interfaceBindTestMethod(Math.PI)"));

            Assert.IsTrue(explicitBaseTestInterface.ExplicitBaseInterfaceProperty.SequenceEqual((int[])engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceProperty")));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceScalarProperty, Convert.ToInt16(engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceScalarProperty")));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceEnumProperty, engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceEnumProperty"));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceStructProperty, engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceStructProperty"));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceReadOnlyProperty, Convert.ToByte(engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceReadOnlyProperty")));

            engine.Execute("var connection = explicitBaseTestInterface.explicitBaseInterfaceEvent.connect(function (sender, args) { explicitBaseTestInterface.explicitBaseInterfaceScalarProperty = args.arg; });");
            arg = makeShort();
            explicitBaseTestInterface.ExplicitBaseInterfaceFireEvent(arg);
            Assert.AreEqual(arg, explicitBaseTestInterface.ExplicitBaseInterfaceScalarProperty);
            engine.Execute("connection.disconnect();");
            explicitBaseTestInterface.ExplicitBaseInterfaceFireEvent(makeShort());
            Assert.AreEqual(arg, explicitBaseTestInterface.ExplicitBaseInterfaceScalarProperty);

            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceMethod("foo", 4), engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceMethod('foo', 4)"));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceMethod("foo", 4, TestEnum.Second), engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceMethod('foo', 4, TestEnum.second)"));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceMethod<TestEnum>(4), engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceMethod(TestEnum, 4)"));
            Assert.AreEqual(explicitBaseTestInterface.ExplicitBaseInterfaceBindTestMethod(Math.PI), engine.Evaluate("explicitBaseTestInterface.explicitBaseInterfaceBindTestMethod(Math.PI)"));

            Assert.IsTrue(explicitTestInterface.ExplicitInterfaceProperty.SequenceEqual((int[])engine.Evaluate("explicitTestInterface.explicitInterfaceProperty")));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceScalarProperty, Convert.ToInt16(engine.Evaluate("explicitTestInterface.explicitInterfaceScalarProperty")));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceEnumProperty, engine.Evaluate("explicitTestInterface.explicitInterfaceEnumProperty"));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceStructProperty, engine.Evaluate("explicitTestInterface.explicitInterfaceStructProperty"));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceReadOnlyProperty, Convert.ToByte(engine.Evaluate("explicitTestInterface.explicitInterfaceReadOnlyProperty")));

            engine.Execute("var connection = explicitTestInterface.explicitInterfaceEvent.connect(function (sender, args) { explicitTestInterface.explicitInterfaceScalarProperty = args.arg; });");
            arg = makeShort();
            explicitTestInterface.ExplicitInterfaceFireEvent(arg);
            Assert.AreEqual(arg, explicitTestInterface.ExplicitInterfaceScalarProperty);
            engine.Execute("connection.disconnect();");
            explicitTestInterface.ExplicitInterfaceFireEvent(makeShort());
            Assert.AreEqual(arg, explicitTestInterface.ExplicitInterfaceScalarProperty);

            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceMethod("foo", 4), engine.Evaluate("explicitTestInterface.explicitInterfaceMethod('foo', 4)"));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceMethod("foo", 4, TestEnum.Second), engine.Evaluate("explicitTestInterface.explicitInterfaceMethod('foo', 4, TestEnum.second)"));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceMethod<TestEnum>(4), engine.Evaluate("explicitTestInterface.explicitInterfaceMethod(TestEnum, 4)"));
            Assert.AreEqual(explicitTestInterface.ExplicitInterfaceBindTestMethod(Math.PI), engine.Evaluate("explicitTestInterface.explicitInterfaceBindTestMethod(Math.PI)"));
        }

        private sealed class CamelCaseAttributeLoader : CustomAttributeLoader
        {
            public override T[] LoadCustomAttributes<T>(ICustomAttributeProvider resource, bool inherit)
            {
                if (typeof(T) == typeof(ScriptMemberAttribute) && (resource is MemberInfo member) && !member.DeclaringType.IsArray && (member.DeclaringType != typeof(Array)))
                {
                    var name = char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1);
                    return new[] { new ScriptMemberAttribute(name) } as T[];
                }

                return base.LoadCustomAttributes<T>(resource, inherit);
            }
        }

        // ReSharper disable UnusedMember.Local

        private void PrivateMethod()
        {
        }

        private static void PrivateStaticMethod()
        {
        }

        private delegate string TestDelegate(string pre, ref string value, int post);

        public sealed class DirectAccessTestObject
        {
            public string Format(string format, object arg0 = null, object arg1 = null, object arg2 = null, object arg3 = null)
            {
                return MiscHelpers.FormatInvariant(format, arg0, arg1, arg2, arg3);
            }

            public T Bogus<T>(T arg)
            {
                return default;
            }
        }

        [ComVisible(true)]
        public sealed class ComVisibleTestObject
        {
            public string Format(string format, object arg0 = null, object arg1 = null, object arg2 = null, object arg3 = null)
            {
                return MiscHelpers.FormatInvariant(format, arg0, arg1, arg2, arg3);
            }

            public T Bogus<T>(T arg)
            {
                return default;
            }
        }

        // ReSharper restore UnusedMember.Local

        #endregion
    }
}
