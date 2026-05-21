// ******************************************************************
// Copyright © 2015-2022 Uno Platform inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ******************************************************************
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Uno.Wasm.Bootstrap;

namespace Uno.Wasm.Bootstrap.UnitTests
{
	[TestClass]
	public class Given_ShellTask_EnvironmentVariableEscaping
	{
		[TestMethod]
		[DataRow("plain value", "plain value")]
		[DataRow("value with \"quotes\"", "value with \\\"quotes\\\"")]
		[DataRow("value with \\backslash", "value with \\\\backslash")]
		[DataRow("{\"sub\":\"7074\"}", "{\\\"sub\\\":\\\"7074\\\"}")]
		[DataRow("back\\slash and \"quote\"", "back\\\\slash and \\\"quote\\\"")]
		[DataRow("", "")]
		[DataRow("line\nfeed", "line\\nfeed")]
		[DataRow("carriage\rreturn", "carriage\\rreturn")]
		[DataRow("tab\there", "tab\\there")]
		[DataRow("ls\u2028sep", "ls\\u2028sep")]
		[DataRow("ps\u2029sep", "ps\\u2029sep")]
		public void When_EscapeJsString_Then_SpecialCharsAreCorrectlyEscaped(string input, string expected)
		{
			var result = JsStringHelper.EscapeJsString(input);
			Assert.AreEqual(expected, result);
		}

		// Regression: even though `EscapeJsString` correctly produces `\"`
		// for `"`, the build pipeline used to corrupt that into `/"` when
		// the uno-config.js fingerprint update target round-tripped the
		// file through MSBuild's `<WriteLinesToFile>`. The MSBuild item-
		// value pipeline normalised backslashes to forward slashes
		// (treating them as path separators), turning legitimate JS
		// escapes into invalid syntax that broke bootstrap loading with
		// `SyntaxError: Unexpected identifier <next-token>` against
		// whatever identifier followed the prematurely-terminated string
		// literal.
		//
		// The fix replaced `<WriteLinesToFile>` with `WriteFileVerbatimTask_v0`
		// (a real task that writes via `System.IO.File.WriteAllText`), whose
		// `string`-typed parameters bypass the item-value pipeline and
		// preserve bytes verbatim. This test asserts the contract
		// `EscapeJsString` must satisfy for that downstream round-trip to
		// remain correct.
		[TestMethod]
		public void When_EscapeJsString_Then_OutputContainsLiteralBackslash()
		{
			var result = JsStringHelper.EscapeJsString("hello\"world");

			Assert.AreEqual("hello\\\"world", result, "EscapeJsString must produce backslash-quote for an input quote.");
			Assert.IsFalse(result.Contains('/'), "EscapeJsString output must not introduce forward slashes (downstream targets must preserve bytes verbatim).");
			Assert.IsTrue(result.Contains('\\'), "Expected literal backslash in the escaped output.");
		}
	}
}
