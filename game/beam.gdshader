// A laser beam (game/Projectiles.cs): a flat strip that turns about its own length to face the camera, a thin hot
// core down the middle and a soft glow out to its edges, added on. Each instance: basis Y = the beam (its length),
// basis X = its width; the instance colour is the glow's, its alpha the fade.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;

varying vec4 tint;

void vertex() {
	vec3 centre = MODEL_MATRIX[3].xyz;
	vec3 along = MODEL_MATRIX[1].xyz;
	float width = length(MODEL_MATRIX[0].xyz);
	vec3 axis = normalize(along);
	vec3 world = centre + along * VERTEX.y;
	vec3 to_cam = normalize(CAMERA_POSITION_WORLD - world);
	vec3 side = normalize(cross(axis, to_cam));
	world += side * VERTEX.x * width;
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world, 1.0);
	tint = COLOR;
}

void fragment() {
	float d = abs(UV.x - 0.5) * 2.0;                       // 0 down the middle, 1 at the edges
	float core = 1.0 - smoothstep(0.18, 0.4, d);
	float glow = pow(1.0 - d, 3.0);
	float ends = smoothstep(0.0, 0.04, UV.y) * smoothstep(1.0, 0.96, UV.y);
	ALBEDO = (tint.rgb * glow * 0.8 + vec3(1.0, 0.95, 0.82) * core * 0.65) * tint.a * ends;
}
