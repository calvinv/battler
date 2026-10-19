// The battlefield's ground, darkened where the local player's side can't see (fog of war). `fog` has one texel per
// world unit across the map, white where seen; Main fills it every tick.
shader_type spatial;

uniform vec3 ground : source_color = vec3(0.46, 0.41, 0.33);
uniform sampler2D fog : filter_linear;
uniform vec2 half_size = vec2(60.0, 40.0);

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	float seen = texture(fog, uv).r;
	ALBEDO = ground * mix(0.36, 1.0, seen);
	ROUGHNESS = 1.0;
}
