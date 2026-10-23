// Ink outline for the cel shaded units: the model drawn again, inside out, pushed out a constant few pixels on screen
// along its smoothed normals (baked into the vertex colours by tools/art unit_parts _export_rig), so corners close.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque;

uniform vec4 ink : source_color = vec4(0.05, 0.05, 0.07, 1.0);
uniform float width_px = 2.0;

void vertex() {
	vec3 n = normalize(COLOR.rgb * 2.0 - 1.0);
	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec3 vn = normalize((MODELVIEW_MATRIX * vec4(n, 0.0)).xyz);
	vec2 dir = (PROJECTION_MATRIX * vec4(vn, 0.0)).xy;
	float len = length(dir);
	if (len > 1e-5) dir /= len;
	clip.xy += dir * width_px * 2.0 / VIEWPORT_SIZE * clip.w;
	POSITION = clip;
}

void fragment() {
	ALBEDO = ink.rgb;
}
