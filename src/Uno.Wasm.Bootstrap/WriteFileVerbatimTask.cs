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

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Uno.Wasm.Bootstrap;

/// <summary>
/// Writes <see cref="Content"/> verbatim (UTF-8, no BOM) to <see cref="Path"/>.
/// </summary>
/// <remarks>
/// Used in place of the built-in <c>WriteLinesToFile</c> task when the content
/// to be written may contain backslash characters that must be preserved.
/// <para>
/// <c>WriteLinesToFile.Lines</c> is typed as <c>ITaskItem[]</c>, so passing
/// content through it routes the value through MSBuild's item-value pipeline.
/// That pipeline normalises backslash (<c>\</c>) to forward slash (<c>/</c>)
/// as if treating it as a path separator, which silently corrupts any
/// non-path content that legitimately uses backslashes — for example, JS
/// string literals containing <c>\"</c> escape sequences. The resulting
/// <c>/"</c> terminates the literal prematurely and produces invalid JS.
/// </para>
/// <para>
/// A <see cref="string"/>-typed parameter on a real task (this class)
/// bypasses the item-list pipeline and is delivered to the task verbatim,
/// so every byte of <see cref="Content"/> lands on disk unchanged.
/// </para>
/// </remarks>
public class WriteFileVerbatimTask_v0 : Microsoft.Build.Utilities.Task
{
	/// <summary>
	/// Destination file path. Parent directories must already exist.
	/// </summary>
	[Required]
	public string Path { get; set; } = "";

	/// <summary>
	/// File content. Written verbatim as UTF-8 without a BOM.
	/// </summary>
	[Required]
	public string Content { get; set; } = "";

	public override bool Execute()
	{
		// UTF-8 with no BOM matches what ShellTask uses when it first writes
		// uno-config.js, so a round-trip through this task doesn't introduce
		// a stray BOM that downstream tooling (asset fingerprinting, pre-
		// compression) would otherwise have to tolerate.
		File.WriteAllText(Path, Content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		return true;
	}
}
