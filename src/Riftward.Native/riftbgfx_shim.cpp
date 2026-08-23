/*
 * Project Riftward - dünner bgfx-C-Shim für den zentralen C#-Interop-Layer.
 *
 * Zweck: bgfx besitzt keine stabile exportierte C-ABI. Diese eigene,
 * extrem kleine Übersetzungseinheit exportiert genau die Aufrufe, die der
 * T-010-Walking-Skeleton-Host benötigt, als flache C-Symbole. Sie enthält
 * keinerlei Spiellogik, keinen Zustand außer den von bgfx verwalteten
 * Handles und keine Speicherverwaltung eigener Objekte.
 *
 * Besitzregeln:
 * - Shader-, Programm- und Vertex-Buffer-Handles gehören bgfx; Freigabe
 *   ausschließlich über die hier exportierten destroy-Funktionen in dieser
 *   Reihenfolge: Programm -> Shader -> Vertex-Buffer -> bgfx-Shutdown.
 * - Die zurückgegebenen Handle-Indizes sind opaque; 0xFFFF ist ungültig.
 * - Shader-Binärdaten werden offline erzeugt (shaderc) und nur geladen;
 *   es findet keine Shaderkompilierung zur Laufzeit statt.
 *
 * ISA-Vertrag: Dieser Code wird ohne ISA-Anhebung ueber die konservative
 * x86-64-v2-Basis hinaus uebersetzt; ISA-anhebende Compileroptionen sind
 * verboten und werden im Native-Build geprueft (docs/PLATTFORMMATRIX.md).
 */

#include "riftbgfx_shim.h"

#include <bgfx/bgfx.h>

#include <cstring>

namespace
{
	constexpr uint16_t RiftInvalidHandle = 0xFFFF;

	uint16_t encodeVertex(bgfx::VertexBufferHandle handle)
	{
		return handle.idx;
	}

	uint16_t encodeShader(bgfx::ShaderHandle handle)
	{
		return handle.idx;
	}

	uint16_t encodeProgram(bgfx::ProgramHandle handle)
	{
		return handle.idx;
	}
} // namespace

extern "C" uint32_t rift_bgfx_api_version(void)
{
	return BGFX_API_VERSION;
}

extern "C" int32_t rift_bgfx_init(const rift_bgfx_init_params_t* params)
{
	if (params == nullptr || params->nwh == nullptr)
	{
		return RIFT_BGFX_ERR_INVALID_PARAM;
	}

	// Ein renderFrame() vor init() schaltet bgfx in den single-threaded Modus:
	// Der GL-Kontext bleibt am aufrufenden Thread aktuell (notwendig für die
	// Diagnoseabfrage) und es entsteht kein zusätzlicher Renderthread.
	bgfx::renderFrame();

	bgfx::Init init;
	init.type = bgfx::RendererType::OpenGL; /* kompiliert mit BGFX_CONFIG_RENDERER_OPENGL=33 */
	init.fallback = false;                  /* kein stiller Backend-Fallback: kontrollierter Fehler */
	init.platformData.ndt = params->ndt;
	init.platformData.nwh = params->nwh;
	init.resolution.width = params->width;
	init.resolution.height = params->height;
	init.resolution.reset = params->resetFlags;

	if (!bgfx::init(init))
	{
		return RIFT_BGFX_ERR_INIT_FAILED;
	}

	return RIFT_BGFX_OK;
}

extern "C" int32_t rift_bgfx_renderer_type(void)
{
	const bgfx::Caps* caps = bgfx::getCaps();

	if (caps == nullptr)
	{
		return RIFT_BGFX_ERR_NOT_INITIALIZED;
	}

	return static_cast<int32_t>(caps->rendererType);
}

namespace
{
	/* Minimale eigene Deklaration statt GL-Headerabhaengigkeit:
	   ABI von glGetString ist seit OpenGL 1.0 stabil; das Symbol kommt aus
	   dem System-GL (libGL/libOpenGL), der Kontext ist im Single-Threaded-
	   Modus von bgfx am aufrufenden Thread aktuell. */
	constexpr uint32_t kGlRenderer = 0x1F01u;
	constexpr uint32_t kGlVersion = 0x1F02u;
	constexpr uint32_t kGlShadingLanguageVersion = 0x8B8Cu;
} // namespace

extern "C" const unsigned char* glGetString(uint32_t name);

extern "C" void rift_bgfx_gl_strings(
	const char** outVersion,
	const char** outRenderer,
	const char** outSlVersion)
{
	if (outVersion != nullptr)
	{
		const unsigned char* value = bgfx::getCaps() == nullptr ? nullptr : glGetString(kGlVersion);
		*outVersion = value == nullptr ? "" : reinterpret_cast<const char*>(value);
	}

	if (outRenderer != nullptr)
	{
		const unsigned char* value = bgfx::getCaps() == nullptr ? nullptr : glGetString(kGlRenderer);
		*outRenderer = value == nullptr ? "" : reinterpret_cast<const char*>(value);
	}

	if (outSlVersion != nullptr)
	{
		const unsigned char* value = bgfx::getCaps() == nullptr ? nullptr : glGetString(kGlShadingLanguageVersion);
		*outSlVersion = value == nullptr ? "" : reinterpret_cast<const char*>(value);
	}
}

extern "C" uint32_t rift_bgfx_gpu_ids(void)
{
	const bgfx::Caps* caps = bgfx::getCaps();

	if (caps == nullptr)
	{
		return 0u;
	}

	return (static_cast<uint32_t>(caps->vendorId) << 16) | static_cast<uint32_t>(caps->deviceId);
}

extern "C" void rift_bgfx_shutdown(void)
{
	bgfx::shutdown();
}

extern "C" uint32_t rift_bgfx_frame(void)
{
	return bgfx::frame();
}

extern "C" uint32_t rift_bgfx_stats_draw_calls(void)
{
	const bgfx::Stats* stats = bgfx::getStats();
	return stats == nullptr ? 0u : stats->numDraw;
}

extern "C" void rift_view_setup(uint8_t viewId, uint32_t clearColorRgba, uint16_t width, uint16_t height)
{
	bgfx::setViewRect(viewId, 0, 0, width, height);
	bgfx::setViewClear(
		viewId,
		BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
		clearColorRgba,
		1.0f,
		0);
}

extern "C" uint16_t rift_tri_create_vertex_buffer(const void* data, uint32_t sizeInBytes)
{
	if (data == nullptr || sizeInBytes == 0u)
	{
		return RiftInvalidHandle;
	}

	/* Feste Layoutvereinbarung des Skeleton-Dreiecks:
	   Position 3x f32 gefolgt von Color0 4x u8 normalisiert (Stride 16 Bytes). */
	bgfx::VertexLayout layout;
	layout.begin()
		.add(bgfx::Attrib::Position, 3, bgfx::AttribType::Float)
		.add(bgfx::Attrib::Color0, 4, bgfx::AttribType::Uint8, true)
		.end();

	const bgfx::Memory* memory = bgfx::copy(data, sizeInBytes);

	if (memory == nullptr)
	{
		return RiftInvalidHandle;
	}

	return encodeVertex(bgfx::createVertexBuffer(memory, layout));
}

extern "C" uint16_t rift_tri_create_shader(const void* data, uint32_t sizeInBytes)
{
	if (data == nullptr || sizeInBytes == 0u)
	{
		return RiftInvalidHandle;
	}

	const bgfx::Memory* memory = bgfx::copy(data, sizeInBytes);

	if (memory == nullptr)
	{
		return RiftInvalidHandle;
	}

	return encodeShader(bgfx::createShader(memory));
}

extern "C" bool rift_tri_shader_is_valid(uint16_t shaderIdx)
{
	return bgfx::isValid(bgfx::ShaderHandle{ shaderIdx });
}

extern "C" uint16_t rift_tri_create_program(uint16_t vertexShaderIdx, uint16_t fragmentShaderIdx)
{
	if (!bgfx::isValid(bgfx::ShaderHandle{ vertexShaderIdx })
		|| !bgfx::isValid(bgfx::ShaderHandle{ fragmentShaderIdx }))
	{
		return RiftInvalidHandle;
	}

	return encodeProgram(
		bgfx::createProgram(
			bgfx::ShaderHandle{ vertexShaderIdx },
			bgfx::ShaderHandle{ fragmentShaderIdx },
			false)); /* Besitz bleibt beim Host: explizite Destroy-Reihenfolge. */
}

extern "C" void rift_tri_destroy_program(uint16_t programIdx)
{
	bgfx::destroy(bgfx::ProgramHandle{ programIdx });
}

extern "C" void rift_tri_destroy_shader(uint16_t shaderIdx)
{
	bgfx::destroy(bgfx::ShaderHandle{ shaderIdx });
}

extern "C" void rift_tri_destroy_vertex_buffer(uint16_t vertexBufferIdx)
{
	bgfx::destroy(bgfx::VertexBufferHandle{ vertexBufferIdx });
}

extern "C" void rift_tri_submit(uint8_t viewId, uint16_t programIdx, uint16_t vertexBufferIdx)
{
	bgfx::setVertexBuffer(0, bgfx::VertexBufferHandle{ vertexBufferIdx });
	bgfx::submit(viewId, bgfx::ProgramHandle{ programIdx });
}
