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

using System.Text;
using Microsoft.Build.Framework;

namespace Uno.Wasm.Bootstrap;

/// <summary>
/// Backports the emscripten GL_UNPACK_ROW_LENGTH fix
/// (emscripten-core/emscripten#21980, shipped in emscripten 3.1.61) into a
/// runtime-generated <c>dotnet.native.*.js</c>, in place, for apps that opt in to
/// a 4GB heap (<c>-s MAXIMUM_MEMORY=4GB</c>).
/// </summary>
/// <remarks>
/// Above the 2GB heap ceiling, WebGL pixel uploads that set GL_UNPACK_ROW_LENGTH
/// (pname 3314) — as SkiaSharp's WebGL renderer does for sub-rectangle
/// texSubImage2D uploads — fail with
/// <c>INVALID_OPERATION: texSubImage2D: ArrayBufferView not big enough for request</c>
/// because the emscripten GL glue sizes the unpack row from the texture width
/// instead of the configured row length. The .NET runtime packs still ship
/// emscripten 3.1.56, so this build-time backport bridges the gap until a runtime
/// ships 3.1.61+.
/// <para>
/// The work is done here, in a task, rather than in MSBuild properties so the
/// ~hundreds-of-KB file content is never routed through property values or task
/// parameters (which MSBuild captures in binary logs). The file is read once and,
/// when applicable, written once. The decision + transform lives in
/// <see cref="WebGLNativeJsPatcher"/> so it can be unit-tested without an engine.
/// </para>
/// </remarks>
public class PatchWebGLNativeJsForMaxMemoryTask_v0 : Microsoft.Build.Utilities.Task
{
	/// <summary>Path to the <c>dotnet.native.*.js</c> to patch.</summary>
	[Required]
	public string File { get; set; } = "";

	/// <summary>True when this invocation wrote the backported fix (used to drop stale compressed siblings).</summary>
	[Output]
	public bool Applied { get; set; }

	public override bool Execute()
	{
		if (!System.IO.File.Exists(File))
		{
			// Discovery / 0-file diagnostics are the caller's responsibility.
			return true;
		}

		switch (WebGLNativeJsPatcher.TryPatch(System.IO.File.ReadAllText(File), out var patched))
		{
			case WebGLNativeJsPatcher.PatchOutcome.Applied:
				System.IO.File.WriteAllText(File, patched, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Applied = true;
				Log.LogMessage(MessageImportance.High, $"[Uno] Backported GL_UNPACK_ROW_LENGTH fix (emscripten #21980) into {File} for 4GB memory support.");
				break;

			case WebGLNativeJsPatcher.PatchOutcome.Drifted:
				Log.LogWarning($"[Uno] The 4GB WebGL fix (emscripten #21980) could NOT be applied to {File}: the expected emscripten GL glue tokens were not found. The runtime's emscripten version likely changed — update the backport in WebGLNativeJsPatcher.");
				break;

			// NoGlue / AlreadyPatched: nothing to do.
		}

		return true;
	}
}
