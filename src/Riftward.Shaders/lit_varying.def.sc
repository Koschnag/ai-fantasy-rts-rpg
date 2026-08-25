vec3 v_wpos        : TEXCOORD0 = vec3(0.0, 0.0, 0.0);
vec3 v_normal      : NORMAL    = vec3(0.0, 1.0, 0.0);
vec4 v_color       : COLOR0    = vec4(1.0, 1.0, 1.0, 1.0);
vec3 v_shadowCoord0 : TEXCOORD1 = vec3(0.0, 0.0, 0.0);
vec3 v_shadowCoord1 : TEXCOORD2 = vec3(0.0, 0.0, 0.0);
vec3 v_shadowCoord2 : TEXCOORD3 = vec3(0.0, 0.0, 0.0);
vec3 v_shadowCoord3 : TEXCOORD4 = vec3(0.0, 0.0, 0.0);

vec3 a_position   : POSITION;
vec4 a_normal     : NORMAL;
vec4 a_color0     : COLOR0;
