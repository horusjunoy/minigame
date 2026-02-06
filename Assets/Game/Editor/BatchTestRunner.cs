using System;
using System.IO;
using System.Xml;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.Editor
{
    public static class BatchTestRunner
    {
        private const string TestLogPrefix = "TEST|";

        public static void RunEditMode()
        {
            RunTests(TestMode.EditMode);
        }

        public static void RunPlayMode()
        {
            RunTests(TestMode.PlayMode);
        }

        private static void RunTests(TestMode mode)
        {
            var settings = new ExecutionSettings(new Filter { testMode = mode })
            {
                runSynchronously = true
            };
            var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Callback(GetResultsPath(), mode));
            runner.Execute(settings);
        }

        private static string GetResultsPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-testResults", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            var repoRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.Combine(repoRoot, "artifacts", "test-results.xml");
        }

        private sealed class Callback : ICallbacks
        {
            private readonly string _resultsPath;
            private readonly TestMode _mode;

            public Callback(string resultsPath, TestMode mode)
            {
                _resultsPath = resultsPath;
                _mode = mode;
            }

            public void RunStarted(ITestAdaptor tests)
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Test run started ({0}).", _mode);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite)
                {
                    return;
                }

                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}{1}|{2}", TestLogPrefix, result.TestStatus, result.FullName);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                WriteResults(result, _resultsPath);

                if (total == 0)
                {
                    Debug.LogError("No tests were executed.");
                    EditorApplication.Exit(1);
                    return;
                }

                EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
            }

            private static void WriteResults(ITestResultAdaptor result, string filePath)
            {
                try
                {
                    var directoryPath = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    using var fileStream = File.CreateText(filePath);
                    var settings = new XmlWriterSettings
                    {
                        Indent = true,
                        NewLineOnAttributes = false
                    };

                    using var xmlWriter = XmlWriter.Create(fileStream, settings);
                    WriteResultsToXml(result, xmlWriter);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Saving result file failed.");
                    Debug.LogException(ex);
                }
            }

            private static void WriteResultsToXml(ITestResultAdaptor result, XmlWriter xmlWriter)
            {
                const string nUnitVersion = "3.5.0.0";
                const string timeFormat = "u";

                var testRunNode = new TNode("test-run");
                testRunNode.AddAttribute("id", "2");
                testRunNode.AddAttribute("testcasecount", (result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount).ToString());
                testRunNode.AddAttribute("result", result.ResultState);
                testRunNode.AddAttribute("total", (result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount).ToString());
                testRunNode.AddAttribute("passed", result.PassCount.ToString());
                testRunNode.AddAttribute("failed", result.FailCount.ToString());
                testRunNode.AddAttribute("inconclusive", result.InconclusiveCount.ToString());
                testRunNode.AddAttribute("skipped", result.SkipCount.ToString());
                testRunNode.AddAttribute("asserts", result.AssertCount.ToString());
                testRunNode.AddAttribute("engine-version", nUnitVersion);
                testRunNode.AddAttribute("clr-version", Environment.Version.ToString());
                testRunNode.AddAttribute("start-time", result.StartTime.ToString(timeFormat));
                testRunNode.AddAttribute("end-time", result.EndTime.ToString(timeFormat));
                testRunNode.AddAttribute("duration", result.Duration.ToString());

                var resultNode = result.ToXml();
                testRunNode.ChildNodes.Add(resultNode);

                testRunNode.WriteTo(xmlWriter);
            }
        }
    }
}
