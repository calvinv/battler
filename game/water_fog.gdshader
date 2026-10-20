// Pond water (the user, 2026-10-20: "higher definition battlefields"): deeper and darker toward the middle, clearer
// and murky green-brown at the shallows, moving ripples that catch the sun, the sky in it at a glance (Fresnel), and
// a broken ring of foam at the shore. UV.x is how far out from the pond's middle a point is, 0 to 1 at the shore
// (game/Terrain.cs builds the discs). Under the same fog of war as the ground.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D fog : filter_linear, repeat_disable;
uniform vec2 half_size = vec2(60.0, 40.0);

varying vec3 world;

float hash(vec2 p) {
	uvec2 q = uvec2(ivec2(floor(p + 0.5)));
	q = q * uvec2(1597334673u, 3812015801u);
	uint n = (q.x ^ q.y) * 1597334673u;
	n ^= n >> 16u;
	return float(n) * (1.0 / 4294967295.0);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}

// Two sets of ripples drifting different ways.
float waves(vec2 p, float t) {
	return noise(p * 1.6 + vec2(t * 0.35, t * 0.2)) * 0.6 + noise(p * 4.0 - vec2(t * 0.5, -t * 0.4)) * 0.4;
}

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	bool inside = uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0;
	float seen = inside ? texture(fog, uv).r : 0.0;
	float out_ = UV.x;   // 0 middle .. 1 shore

	vec3 deep = vec3(0.025, 0.06, 0.075), shallow = vec3(0.1, 0.13, 0.1);
	vec3 col = mix(deep, shallow, smoothstep(0.45, 1.0, out_));
	// Foam: a broken ring just inside the shore, stirred by the ripples.
	float t = TIME;
	float foam = smoothstep(0.86, 0.97, out_) * smoothstep(0.35, 0.65, noise(world.xz * 2.5 + t * 0.3));
	col = mix(col, vec3(0.55, 0.58, 0.55), foam * 0.6);

	// Ripple normals, carried into view space.
	float e = 0.05;
	float h = waves(world.xz, t);
	float dx = (waves(world.xz + vec2(e, 0.0), t) - h) / e;
	float dz = (waves(world.xz + vec2(0.0, e), t) - h) / e;
	NORMAL = normalize(NORMAL + (VIEW_MATRIX * vec4(-dx * 0.18, 0.0, -dz * 0.18, 0.0)).xyz);

	// The sky in it, more at a glance than looking straight down.
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	col = mix(col, vec3(0.3, 0.38, 0.45), 0.1 + fresnel * 0.5);   // (a little sky even looking down)

	ALBEDO = col * mix(0.32, 1.0, seen) * (inside ? 1.0 : 0.65);
	ROUGHNESS = mix(0.18, 0.6, foam);
	SPECULAR = 0.35;
	ALPHA = mix(0.75, 0.95, 1.0 - out_) * smoothstep(1.0, 0.9, out_);   // fading out at the rim: a soft shore
}
