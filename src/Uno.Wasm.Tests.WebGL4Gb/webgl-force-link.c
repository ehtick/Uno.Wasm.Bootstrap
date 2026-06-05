#include <emscripten.h>
#include <GLES2/gl2.h>

// Forces emscripten to link its WebGL JS glue (library_webgl.js) into
// dotnet.native.js, so the GL_UNPACK_ROW_LENGTH backport target
// (_UnoPatchWebGLNativeJsForMaxMemory) has the real emscripten code to patch.
// A minimal app references no GL symbol, so without this the glue is not
// emitted and the test would validate nothing. EMSCRIPTEN_KEEPALIVE keeps the
// function (and therefore its GL references) through dead-code elimination.
EMSCRIPTEN_KEEPALIVE void uno_webgl_force_link(void)
{
	glPixelStorei(GL_UNPACK_ALIGNMENT, 4);
	glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, (const void*)0);
}
