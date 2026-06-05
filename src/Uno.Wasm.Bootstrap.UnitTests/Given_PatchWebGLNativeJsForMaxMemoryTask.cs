// ******************************************************************
// Copyright © 2015-2026 Uno Platform inc. All rights reserved.
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
	public class Given_PatchWebGLNativeJsForMaxMemoryTask
	{
		// Minimal stand-in for the emscripten GL glue tokens the backport rewrites.
		private const string GlueWithSourceTokens =
			"function _glPixelStorei(pname, param) { if (pname == 3317) { GL.unpackAlignment = param;\n }}\n" +
			"function emscriptenWebGLGetTexPixelData(width, height) {\n var plainRowSize = width * sizePerPixel;\n }\n";

		[TestMethod]
		public void When_GlueAndSourceTokensPresent_Then_Applied()
		{
			var outcome = WebGLNativeJsPatcher.TryPatch(GlueWithSourceTokens, out var patched);

			Assert.AreEqual(WebGLNativeJsPatcher.PatchOutcome.Applied, outcome);
			StringAssert.Contains(patched, "GL.unpackRowLength = param");
			StringAssert.Contains(patched, "(GL.unpackRowLength || width) * sizePerPixel");
			// The original (unpatched) row-size token must be gone.
			Assert.IsFalse(patched.Contains("var plainRowSize = width * sizePerPixel;"));
		}

		[TestMethod]
		public void When_AlreadyPatched_Then_NoOp()
		{
			WebGLNativeJsPatcher.TryPatch(GlueWithSourceTokens, out var patched);

			var outcome = WebGLNativeJsPatcher.TryPatch(patched, out var second);

			Assert.AreEqual(WebGLNativeJsPatcher.PatchOutcome.AlreadyPatched, outcome);
			Assert.AreEqual(patched, second);
		}

		[TestMethod]
		public void When_NoWebGlGlue_Then_NoGlue()
		{
			// The pre-relink stock runtime (and non-WebGL apps) contain no GL glue.
			const string stock = "var x = 1; function monitorRunDependencies(){} export const y = 2;\n";

			var outcome = WebGLNativeJsPatcher.TryPatch(stock, out var patched);

			Assert.AreEqual(WebGLNativeJsPatcher.PatchOutcome.NoGlue, outcome);
			Assert.AreEqual(stock, patched);
		}

		[TestMethod]
		public void When_GluePresentButTokensDrifted_Then_Drifted()
		{
			// Contains the glue sentinel (unpackAlignment) but the exact rewritten tokens
			// have changed — i.e. a future emscripten altered the glue text.
			const string drift = "GL.unpackAlignment = parameter;\nvar rowBytes = width * bytesPerPixel;\n";

			var outcome = WebGLNativeJsPatcher.TryPatch(drift, out var patched);

			Assert.AreEqual(WebGLNativeJsPatcher.PatchOutcome.Drifted, outcome);
			Assert.AreEqual(drift, patched);
		}

		[TestMethod]
		public void When_OnlyFirstTokenMatches_Then_Drifted()
		{
			// First replacement would land but the row-size token changed: a partial match
			// must NOT be written (it would leave the file half-patched and still broken).
			const string partial =
				"GL.unpackAlignment = param;\nvar rowBytes = width * bytesPerPixel;\n";

			var outcome = WebGLNativeJsPatcher.TryPatch(partial, out var patched);

			Assert.AreEqual(WebGLNativeJsPatcher.PatchOutcome.Drifted, outcome);
			Assert.AreEqual(partial, patched);
		}
	}
}
