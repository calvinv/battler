// The battlefield's ground (game/Terrain.cs: its colours are in the vertices), darkened where the local player's side
// can't see (fog of war). `fog` has FogRes texels per world unit across the map, white where seen; Main fills it every
// tick. Past the map's edge it's darker still.
shader_type spatial;

uniform sampler2D fog : filter_linear, repeat_disable;
uniform vec2 half_size = vec2(60.0, 40.0);

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	bool inside = uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0;
	float seen = inside ? texture(fog, uv).r : 0.0;
	vec3 ground = pow(COLOR.rgb, vec3(2.2));   // vertex colours are given as seen (sRGB); lighting wants them linear
	ALBEDO = ground * mix(0.36, 1.0, seen) * (inside ? 1.0 : 0.7);
	ROUGHNESS = 1.0;
}
