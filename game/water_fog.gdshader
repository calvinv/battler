// Pond water: murky and a little see-through, under the same fog of war as the ground (ground_fog.gdshader).
shader_type spatial;

uniform sampler2D fog : filter_linear, repeat_disable;
uniform vec2 half_size = vec2(60.0, 40.0);
uniform vec3 water : source_color = vec3(0.2, 0.27, 0.27);

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	bool inside = uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0;
	float seen = inside ? texture(fog, uv).r : 0.0;
	ALBEDO = water * mix(0.36, 1.0, seen) * (inside ? 1.0 : 0.7);
	ROUGHNESS = 0.2;
	SPECULAR = 0.7;
	ALPHA = 0.85;
}
