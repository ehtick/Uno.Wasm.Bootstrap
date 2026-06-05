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

namespace Uno.Wasm.Bootstrap;

/// <summary>
/// Pure (engine-free) implementation of the emscripten GL_UNPACK_ROW_LENGTH
/// backport applied to <c>dotnet.native.*.js</c>. Kept separate from the MSBuild
/// task so the decision + transform can be unit-tested without an MSBuild engine
/// or a reference to Microsoft.Build.* assemblies.
/// </summary>
internal static class WebGLNativeJsPatcher
{
	// Sentinel proving emscripten's WebGL glue is actually linked into the file. It
	// is not one of the rewritten tokens, so it stays valid across the drift the
	// self-verification guards against. The pre-relink stock runtime (seen as the
	// build output during publish) and non-WebGL apps do not contain it.
	private const string GlueSentinel = "unpackAlignment";

	// Markers proving each replacement landed (paren-free, so tests/callers can match them).
	private const string Marker = "GL.unpackRowLength = param";
	private const string RowMarker = "GL.unpackRowLength || width";

	private const string SourceAlignment = "GL.unpackAlignment = param;";
	private const string PatchedAlignment = "GL.unpackAlignment = param; } else if (pname == 3314) /* GL_UNPACK_ROW_LENGTH */ { GL.unpackRowLength = param;";
	private const string SourceRowSize = "var plainRowSize = width * sizePerPixel;";
	private const string PatchedRowSize = "var plainRowSize = (GL.unpackRowLength || width) * sizePerPixel;";

	/// <summary>Outcome of attempting the backport on a given file content.</summary>
	internal enum PatchOutcome
	{
		/// <summary>The content does not contain emscripten's WebGL glue; nothing to do.</summary>
		NoGlue,

		/// <summary>The fix marker is already present (a prior run, or emscripten &gt;= 3.1.61).</summary>
		AlreadyPatched,

		/// <summary>Both replacements matched; the patched content is returned.</summary>
		Applied,

		/// <summary>Glue is present but the expected tokens were not — the backport is stale.</summary>
		Drifted,
	}

	/// <summary>
	/// Decides and (when applicable) produces the patched content.
	/// <paramref name="patched"/> only differs from <paramref name="content"/> when
	/// the outcome is <see cref="PatchOutcome.Applied"/>.
	/// </summary>
	internal static PatchOutcome TryPatch(string content, out string patched)
	{
		patched = content;

		if (!content.Contains(GlueSentinel))
		{
			return PatchOutcome.NoGlue;
		}

		if (content.Contains(Marker))
		{
			return PatchOutcome.AlreadyPatched;
		}

		var candidate = content
			.Replace(SourceAlignment, PatchedAlignment)
			.Replace(SourceRowSize, PatchedRowSize);

		// Only treat as applied when BOTH replacements landed; a partial match would
		// leave the file half-patched (and still broken) so it must not be written.
		if (candidate.Contains(Marker) && candidate.Contains(RowMarker))
		{
			patched = candidate;
			return PatchOutcome.Applied;
		}

		return PatchOutcome.Drifted;
	}
}
