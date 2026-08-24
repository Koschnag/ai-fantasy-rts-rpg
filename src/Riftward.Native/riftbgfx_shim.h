/*
 * Project Riftward - öffentliche Grenze des bgfx-C-Shims.
 * Nur dieser Header beschreibt die ABI zwischen libriftbgfx.so und dem
 * C#-Interop-Layer (Riftward.Platform). Änderungen sind eine bewusste,
 * dokumentierte Vertragsänderung.
 */

#ifndef RIFTWARD_BGFX_SHIM_H
#define RIFTWARD_BGFX_SHIM_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

enum
{
	RIFT_BGFX_OK = 0,
	RIFT_BGFX_ERR_INVALID_PARAM = -1,
	RIFT_BGFX_ERR_INIT_FAILED = -2,
	RIFT_BGFX_ERR_NOT_INITIALIZED = -3
};

typedef struct rift_bgfx_init_params_t
{
	void* ndt;             /* Native Display (Wayland wl_display* oder NULL für EGL-Default/X11). */
	void* nwh;             /* Native Window: X11 Window-ID als Zeiger oder wl_surface*. */
	uint32_t width;
	uint32_t height;
	uint32_t resetFlags;   /* bgfx-Reset-Bits, z. B. VSync (0x80). */
} rift_bgfx_init_params_t;

uint32_t rift_bgfx_api_version(void);
int32_t rift_bgfx_init(const rift_bgfx_init_params_t* params);
int32_t rift_bgfx_renderer_type(void); /* bgfx::RendererType::Enum; 8 == OpenGL */
void rift_bgfx_gl_strings(
	const char** outVersion,       /* GL_VERSION inkl. Mesa-/Treiberversion. */
	const char** outRenderer,      /* GL_RENDERER (GPU-Bezeichnung). */
	const char** outSlVersion);    /* GL_SHADING_LANGUAGE_VERSION. */
uint32_t rift_bgfx_gpu_ids(void);      /* hi16 vendorId, lo16 deviceId */
void rift_bgfx_shutdown(void);
uint32_t rift_bgfx_frame(void);
uint32_t rift_bgfx_stats_draw_calls(void); /* Draw-Aufrufe des letzten Frames aus bgfx::Stats. */

void rift_view_setup(uint8_t viewId, uint32_t clearColorRgba, uint16_t width, uint16_t height);

/* Feste Layoutvereinbarung: Position 3x f32 + Color0 4x u8 normalisiert. */
uint16_t rift_tri_create_vertex_buffer(const void* data, uint32_t sizeInBytes);
uint16_t rift_tri_create_shader(const void* data, uint32_t sizeInBytes);
bool rift_tri_shader_is_valid(uint16_t shaderIdx);
uint16_t rift_tri_create_program(uint16_t vertexShaderIdx, uint16_t fragmentShaderIdx);

/* Freigabereihenfolge ist verpflichtend: Programm, dann Shader, dann Vertex-Buffer. */
void rift_tri_destroy_program(uint16_t programIdx);
void rift_tri_destroy_shader(uint16_t shaderIdx);
void rift_tri_destroy_vertex_buffer(uint16_t vertexBufferIdx);

void rift_tri_submit(uint8_t viewId, uint16_t programIdx, uint16_t vertexBufferIdx);

#ifdef __cplusplus
} // extern "C"
#endif

#endif /* RIFTWARD_BGFX_SHIM_H */
