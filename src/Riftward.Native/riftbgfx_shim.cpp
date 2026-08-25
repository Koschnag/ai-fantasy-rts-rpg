/*
 * Project Riftward - dünner bgfx-C-Shim für den zentralen C#-Interop-Layer.
 *
 * Zweck: bgfx besitzt keine stabile exportierte C-ABI. Diese eigene,
 * extrem kleine Übersetzungseinheit exportiert genau die Aufrufe, die der
 * T-010-Walking-Skeleton-Host und die Benchmark-Szenarien (T-020 leere
 * Szene, T-023 integrierter Belastungsframe) benötigen, als flache
 * C-Symbole. Sie enthält keinerlei Spiellogik, keinen Zustand außer den von
 * bgfx verwalteten Handles und keine Speicherverwaltung eigener Objekte.
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

extern "C" uint64_t rift_bgfx_caps(void)
{
	const bgfx::Caps* caps = bgfx::getCaps();

	return caps == nullptr ? 0u : caps->supported;
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

extern "C" int32_t rift_bgfx_stats_snapshot(rift_bgfx_stats_t* out)
{
	if (out == nullptr)
	{
		return RIFT_BGFX_ERR_INVALID_PARAM;
	}

	const bgfx::Stats* stats = bgfx::getStats();

	if (stats == nullptr)
	{
		return RIFT_BGFX_ERR_NOT_INITIALIZED;
	}

	out->numDraw           = stats->numDraw;
	out->numCompute        = stats->numCompute;
	out->trianglesRendered = stats->numPrims[0]; /* bgfx::Topology::TriList */
	out->gpuTimeBegin      = stats->gpuTimeBegin;
	out->gpuTimeEnd        = stats->gpuTimeEnd;
	out->gpuTimerFreq      = stats->gpuTimerFreq;
	out->textureMemoryUsed = stats->textureMemoryUsed;
	out->rtMemoryUsed      = stats->rtMemoryUsed;
	out->transientVbUsed   = stats->transientVbUsed;
	out->transientIbUsed   = stats->transientIbUsed;

	return RIFT_BGFX_OK;
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

extern "C" void rift_view_transform(uint8_t viewId, const float* view16, const float* proj16)
{
	if (view16 == nullptr || proj16 == nullptr)
	{
		return;
	}

	bgfx::setViewTransform(viewId, view16, proj16);
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

/* -------------------------------------------------------------- T-023-Erweiterung */

namespace
{
	constexpr uint16_t encodeTexture(bgfx::TextureHandle handle)
	{
		return handle.idx;
	}

	constexpr uint16_t encodeFrameBuffer(bgfx::FrameBufferHandle handle)
	{
		return handle.idx;
	}

	constexpr uint16_t encodeUniform(bgfx::UniformHandle handle)
	{
		return handle.idx;
	}

	constexpr uint16_t encodeIndexBuffer(bgfx::IndexBufferHandle handle)
	{
		return handle.idx;
	}

	bgfx::TextureFormat::Enum toTextureFormat(int32_t format)
	{
		switch (format)
		{
			case RIFT_TEX_FMT_RGBA32F:
				return bgfx::TextureFormat::RGBA32F;
			case RIFT_TEX_FMT_RGBA8:
			default:
				return bgfx::TextureFormat::RGBA8;
		}
	}

	bgfx::UniformType::Enum toUniformType(int32_t type)
	{
		switch (type)
		{
			case RIFT_UNIFORM_MAT4:
				return bgfx::UniformType::Mat4;
			case RIFT_UNIFORM_SAMPLER:
				return bgfx::UniformType::Sampler;
			case RIFT_UNIFORM_VEC4:
			default:
				return bgfx::UniformType::Vec4;
		}
	}
} // namespace

extern "C" uint16_t rift_tex_create_2d(uint16_t width, uint16_t height, int32_t format, uint64_t flags,
	const void* data, uint32_t sizeInBytes)
{
	const bgfx::Memory* memory = nullptr;

	if (data != nullptr && sizeInBytes != 0u)
	{
		memory = bgfx::copy(data, sizeInBytes);

		if (memory == nullptr)
		{
			return RiftInvalidHandle;
		}
	}

	return encodeTexture(
		bgfx::createTexture2D(width, height, false, 1, toTextureFormat(format), flags, memory));
}

extern "C" void rift_tex_update_2d(uint16_t texIdx, uint16_t x, uint16_t y, uint16_t width, uint16_t height,
	const void* data, uint32_t sizeInBytes, uint16_t pitch)
{
	if (data == nullptr || sizeInBytes == 0u)
	{
		return;
	}

	const bgfx::Memory* memory = bgfx::copy(data, sizeInBytes);

	if (memory == nullptr)
	{
		return;
	}

	bgfx::updateTexture2D(
		bgfx::TextureHandle{ texIdx }, 0, 0, x, y, width, height, memory, pitch);
}

extern "C" void rift_tex_destroy(uint16_t texIdx)
{
	bgfx::destroy(bgfx::TextureHandle{ texIdx });
}

extern "C" uint16_t rift_fb_create_single(uint16_t texIdx)
{
	bgfx::TextureHandle texture{ texIdx };
	return encodeFrameBuffer(bgfx::createFrameBuffer(1, &texture, false));
}

extern "C" void rift_fb_destroy(uint16_t fbIdx)
{
	bgfx::destroy(bgfx::FrameBufferHandle{ fbIdx });
}

extern "C" void rift_view_frame_buffer(uint8_t viewId, uint16_t fbIdx)
{
	if (fbIdx == RiftInvalidHandle)
	{
		bgfx::setViewFrameBuffer(viewId, BGFX_INVALID_HANDLE);
		return;
	}

	bgfx::setViewFrameBuffer(viewId, bgfx::FrameBufferHandle{ fbIdx });
}

extern "C" void rift_blit_full(uint8_t viewId, uint16_t dstIdx, uint16_t srcIdx, uint16_t width, uint16_t height)
{
	bgfx::blit(
		viewId,
		bgfx::TextureHandle{ dstIdx },
		0,
		0,
		bgfx::TextureHandle{ srcIdx },
		0,
		0,
		width,
		height);
}

extern "C" uint32_t rift_read_texture_begin(uint16_t texIdx, void* outBuffer, uint32_t bufferSizeBytes)
{
	if (outBuffer == nullptr || bufferSizeBytes == 0u)
	{
		return 0u;
	}

	return bgfx::readTexture(bgfx::TextureHandle{ texIdx }, outBuffer);
}

extern "C" uint16_t rift_uniform_create(const char* name, int32_t type, uint16_t count)
{
	if (name == nullptr || count == 0u)
	{
		return RiftInvalidHandle;
	}

	return encodeUniform(bgfx::createUniform(name, toUniformType(type), count));
}

extern "C" void rift_uniform_destroy(uint16_t uniformIdx)
{
	bgfx::destroy(bgfx::UniformHandle{ uniformIdx });
}

extern "C" void rift_set_uniform_vec4(uint16_t uniformIdx, const float* values, uint16_t count)
{
	if (values == nullptr)
	{
		return;
	}

	bgfx::setUniform(bgfx::UniformHandle{ uniformIdx }, values, count);
}

extern "C" void rift_set_uniform_mat4(uint16_t uniformIdx, const float* values16)
{
	if (values16 == nullptr)
	{
		return;
	}

	bgfx::setUniform(bgfx::UniformHandle{ uniformIdx }, values16);
}

extern "C" void rift_set_texture(uint8_t stage, uint16_t uniformIdx, uint16_t texIdx, uint32_t samplerFlags)
{
	bgfx::setTexture(stage, bgfx::UniformHandle{ uniformIdx }, bgfx::TextureHandle{ texIdx }, samplerFlags);
}

extern "C" uint16_t rift_ib_create(const void* data, uint32_t sizeInBytes, int32_t uint32Indices)
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

	const uint16_t flags = uint32Indices != 0 ? static_cast<uint16_t>(BGFX_BUFFER_INDEX32)
											  : static_cast<uint16_t>(BGFX_BUFFER_NONE);

	return encodeIndexBuffer(bgfx::createIndexBuffer(memory, flags));
}

extern "C" uint16_t rift_vb_create_layout(const void* data, uint32_t sizeInBytes, uint8_t layoutId)
{
	if (data == nullptr || sizeInBytes == 0u)
	{
		return RiftInvalidHandle;
	}

	bgfx::VertexLayout layout;

	switch (layoutId)
	{
		case 0:
			layout.begin()
				.add(bgfx::Attrib::Position, 3, bgfx::AttribType::Float)
				.add(bgfx::Attrib::Normal, 4, bgfx::AttribType::Uint8, true)
				.add(bgfx::Attrib::Indices, 4, bgfx::AttribType::Uint8)
				.add(bgfx::Attrib::Weight, 4, bgfx::AttribType::Uint8, true)
				.end();
			break;

		case 1:
			layout.begin()
				.add(bgfx::Attrib::Position, 3, bgfx::AttribType::Float)
				.add(bgfx::Attrib::Normal, 4, bgfx::AttribType::Uint8, true)
				.add(bgfx::Attrib::Color0, 4, bgfx::AttribType::Uint8, true)
				.end();
			break;

		case 2:
			layout.begin()
				.add(bgfx::Attrib::Position, 2, bgfx::AttribType::Float)
				.add(bgfx::Attrib::TexCoord0, 2, bgfx::AttribType::Float)
				.end();
			break;

		default:
			return RiftInvalidHandle;
	}

	const bgfx::Memory* memory = bgfx::copy(data, sizeInBytes);

	if (memory == nullptr)
	{
		return RiftInvalidHandle;
	}

	return encodeVertex(bgfx::createVertexBuffer(memory, layout));
}

extern "C" void rift_ib_destroy(uint16_t ibIdx)
{
	bgfx::destroy(bgfx::IndexBufferHandle{ ibIdx });
}

extern "C" void rift_draw_submit(uint8_t viewId, uint16_t programIdx,
	uint16_t vertexBufferIdx, uint16_t indexBufferIdx, uint32_t elementCount,
	const void* instanceData, uint32_t instanceCount,
	uint16_t instanceStride, uint64_t state)
{
	if (vertexBufferIdx == RiftInvalidHandle
		|| (instanceData == nullptr && instanceCount != 0u)
		|| (instanceData != nullptr && (instanceStride % 16u) != 0u))
	{
		return; /* Vertrag: Instanzstride muss Vielfaches von 16 sein. */
	}

	bgfx::setState(state);

	if (indexBufferIdx != RiftInvalidHandle)
	{
		bgfx::setIndexBuffer(bgfx::IndexBufferHandle{ indexBufferIdx }, 0u, elementCount);
		bgfx::setVertexBuffer(0, bgfx::VertexBufferHandle{ vertexBufferIdx });
	}
	else
	{
		bgfx::setVertexBuffer(0, bgfx::VertexBufferHandle{ vertexBufferIdx }, 0u, elementCount);
	}

	if (instanceData != nullptr && instanceCount > 0u)
	{
		bgfx::InstanceDataBuffer idb;
		bgfx::allocInstanceDataBuffer(&idb, instanceCount, instanceStride);

		if (idb.data == nullptr)
		{
			return;
		}

		memcpy(idb.data, instanceData, idb.size);
		bgfx::setInstanceDataBuffer(&idb);
	}

	bgfx::submit(viewId, bgfx::ProgramHandle{ programIdx });
}
