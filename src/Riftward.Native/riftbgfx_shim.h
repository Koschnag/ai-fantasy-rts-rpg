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
uint64_t rift_bgfx_caps(void);         /* bgfx::Caps::supported-Bitmaske */
void rift_bgfx_shutdown(void);
uint32_t rift_bgfx_frame(void);
uint32_t rift_bgfx_stats_draw_calls(void); /* Draw-Aufrufe des letzten Frames aus bgfx::Stats. */

/*
 * T-020-Telemetrie: flache Momentaufnahme von bgfx::Stats des letzten Frames.
 * Alle Werte sind von bgfx gemessene Groessen; das Struct enthaelt keine
 * eigenen Berechnungen. gpuTimerFreq == 0 bedeutet: Das aktive Backend stellt
 * keine GPU-Zeiterfassung bereit; dann sind gpuTimeBegin/gpuTimeEnd ungueltig.
 */
typedef struct rift_bgfx_stats_t
{
	uint32_t numDraw;              /* bgfx::Stats::numDraw */
	uint32_t numCompute;           /* bgfx::Stats::numCompute */
	uint32_t trianglesRendered;    /* bgfx::Stats::numPrims[TriList] */
	int64_t gpuTimeBegin;          /* bgfx::Stats::gpuTimeBegin */
	int64_t gpuTimeEnd;            /* bgfx::Stats::gpuTimeEnd */
	int64_t gpuTimerFreq;          /* bgfx::Stats::gpuTimerFreq (0 == nicht verfuegbar) */
	int64_t textureMemoryUsed;     /* bgfx::Stats::textureMemoryUsed (Bytes) */
	int64_t rtMemoryUsed;          /* bgfx::Stats::rtMemoryUsed (Bytes) */
	int32_t transientVbUsed;       /* bgfx::Stats::transientVbUsed (Bytes) */
	int32_t transientIbUsed;       /* bgfx::Stats::transientIbUsed (Bytes) */
} rift_bgfx_stats_t;

/* Liefert RIFT_BGFX_OK bei erfolgreicher Momentaufnahme; out bleibt sonst unveraendert. */
int32_t rift_bgfx_stats_snapshot(rift_bgfx_stats_t* out);

void rift_view_setup(uint8_t viewId, uint32_t clearColorRgba, uint16_t width, uint16_t height);

/* Setzt View-/Projektionsmatrix eines Views (je 16 floats im bx-Speicherlayout). */
void rift_view_transform(uint8_t viewId, const float* view16, const float* proj16);

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

/*
 * T-023-Erweiterung (integrierter Belastungsframe): Instancing, Uniforms,
 * Texturen, Schatten-/Capture-Renderziele und GPU-Readback. Die Erweiterung
 * folgt demselben Reproduzierbarkeitsvertrag wie der Grundumfang; es findet
 * weiterhin keine Shaderkompilierung zur Laufzeit statt.
 */

/* Eigene Formatkennungen des Shims; Abbildung bleibt bgfx-intern. */
enum
{
	RIFT_TEX_FMT_RGBA8 = 0,
	RIFT_TEX_FMT_RGBA32F = 1
};

/* bgfx::UniformType-Kennungen fuer die Shim-Grenze. */
enum
{
	RIFT_UNIFORM_VEC4 = 0,
	RIFT_UNIFORM_MAT4 = 1,
	RIFT_UNIFORM_SAMPLER = 2
};

/* Ungueltige Handleindizes bleiben 0xFFFF. */
#define RIFT_INVALID_HANDLE 0xFFFFu

/*
 * Erstellt eine Textur. data darf NULL sein (uninitialisiertes RT);
 * sonst wird sizeInBytes als initiale Uploadgroesse kopiert.
 */
uint16_t rift_tex_create_2d(uint16_t width, uint16_t height, int32_t format, uint64_t flags,
	const void* data, uint32_t sizeInBytes);

/* Teilbereichsupdate einer dynamischen Textur; pitch ist die Zeilenbreite in Bytes. */
void rift_tex_update_2d(uint16_t texIdx, uint16_t x, uint16_t y, uint16_t width, uint16_t height,
	const void* data, uint32_t sizeInBytes, uint16_t pitch);

void rift_tex_destroy(uint16_t texIdx);

/* Framebuffer aus genau einer Farbtextur (RT); Freigabe vor der Textur. */
uint16_t rift_fb_create_single(uint16_t texIdx);
void rift_fb_destroy(uint16_t fbIdx);

/* Bindet einen Framebuffer an einen View; 0xFFFF loest die Bindung. */
void rift_view_frame_buffer(uint8_t viewId, uint16_t fbIdx);

/* Vollflaechiger Blit einer Textur in eine andere (gleiche Groesse erforderlich). */
void rift_blit_full(uint8_t viewId, uint16_t dstIdx, uint16_t srcIdx, uint16_t width, uint16_t height);

/* Fordert ein Readback an; Rueckgabe ist der Frame, ab dem die Daten gueltig sind. */
uint32_t rift_read_texture_begin(uint16_t texIdx, void* outBuffer, uint32_t bufferSizeBytes);

uint16_t rift_uniform_create(const char* name, int32_t type, uint16_t count);
void rift_uniform_destroy(uint16_t uniformIdx);
void rift_set_uniform_vec4(uint16_t uniformIdx, const float* values, uint16_t count);
void rift_set_uniform_mat4(uint16_t uniformIdx, const float* values16);

/* Bindet eine Textur an eine Sampler-Uniform eines Stages. */
void rift_set_texture(uint8_t stage, uint16_t uniformIdx, uint16_t texIdx, uint32_t samplerFlags);

/* Erstellt einen statischen Index-Buffer; uint32Indices != 0 waehlt 32-Bit-Indizes. */
uint16_t rift_ib_create(const void* data, uint32_t sizeInBytes, int32_t uint32Indices);
void rift_ib_destroy(uint16_t ibIdx);

/*
 * Erstellt einen Vertex-Buffer mit einem der festen Graybox-Layouts:
 * 0 = Einheitenmesh (pos 3xf32, normal 4xu8n, indices 4xu8, weight 4xu8n),
 * 1 = Landschaft (pos 3xf32, normal 4xu8n, color0 4xu8n),
 * 2 = Partikelquad (pos 2xf32, texcoord0 2xf32).
 */
uint16_t rift_vb_create_layout(const void* data, uint32_t sizeInBytes, uint8_t layoutId);

/*
 * Flexibler Draw-Submit: Vertex- und optionaler Index-Buffer, optional
 * Instanzdaten (Stride muss Vielfaches von 16 sein), Renderzustand.
 * instanceData == NULL bedeutet nicht instanziert; dann zaehlt
 * elementCount Vertices, sonst Instanzen bzw. Indizes je nach Index-Buffer.
 */
void rift_draw_submit(uint8_t viewId, uint16_t programIdx,
	uint16_t vertexBufferIdx, uint16_t indexBufferIdx, uint32_t elementCount,
	const void* instanceData, uint32_t instanceCount,
	uint16_t instanceStride, uint64_t state);

#ifdef __cplusplus
} // extern "C"
#endif

#endif /* RIFTWARD_BGFX_SHIM_H */
