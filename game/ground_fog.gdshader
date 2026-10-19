// The battlefield's ground (game/Terrain.cs: its broad colours are in the vertices), darkened where the local player's
// side can't see (fog of war). `fog` has FogRes texels per world unit across the map, white where seen; Main fills it
// every tick. Past the map's edge it's darker still.
// Close-up detail is made here from world-space noise (the user, 2026-10-19: "have the ground be a bit more
// detailled"): broad patches of darker and paler soil, scrubby grass in the low ground, gravel speckle, and small
// bumps that catch the light.
shader_type spatial;

uniform sampler2D fog : filter_linear, repeat_disable;
uniform vec2 half_size = vec2(60.0, 40.0);

varying vec3 world;

// A random 0..1 for a whole-number lattice point. Integer maths, so two neighbouring cells agree exactly on the
// corner they share (a float hash differed by rounding there and drew seams along the lattice).
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

float fbm(vec2 p) {
	float s = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { s += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
	return s;
}

// Little bumps: the height of the fine relief, for the normal.
float bumps(vec2 p) {
	return noise(p * 2.2) * 0.6 + noise(p * 6.5) * 0.3 + noise(p * 15.0) * 0.1;
}

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	bool inside = uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0;
	float seen = inside ? texture(fog, uv).r : 0.0;
	vec3 ground = pow(COLOR.rgb, vec3(2.2));   // vertex colours are given as seen (sRGB); lighting wants them linear

	vec2 p = world.xz;
	// Broad patches: some soil darker and damper, some paler and dry.
	float patch = fbm(p * 0.07);
	ground *= mix(0.78, 1.18, smoothstep(0.25, 0.75, patch));
	// Scrub: tufts of dull grass where the ground is low and the noise says so.
	float low = clamp(-world.y / 1.5 + 0.35, 0.0, 1.0);
	float scrub = smoothstep(0.55, 0.7, fbm(p * 0.35 + 3.0)) * low;
	float blades = noise(p * 9.0);
	ground = mix(ground, vec3(0.075, 0.085, 0.035) * (0.7 + 0.6 * blades), scrub * 0.75);
	// Mottling at a metre or so, and gravel: scattered pale and dark stones.
	ground *= 0.88 + 0.24 * noise(p * 1.3);
	// Pebbles: in a grid of small cells, some cells hold a round stone of random size somewhere inside.
	vec2 cell = floor(p * 2.5), in_cell = fract(p * 2.5);
	float pick = hash(cell);
	if (pick < 0.22) {
		vec2 centre = vec2(hash(cell + vec2(7, 1)), hash(cell + vec2(3, 11))) * 0.6 + 0.2;
		float radius = 0.08 + 0.14 * hash(cell + vec2(19, 5));
		float d = length(in_cell - centre);
		float pebble = smoothstep(radius, radius * 0.6, d);
		vec3 tone = pick < 0.08 ? vec3(0.17, 0.16, 0.14) : ground * 0.55;   // pale stone or dark clod
		ground = mix(ground, tone * (0.85 + 0.3 * (centre.y - in_cell.y + 0.5)), pebble * 0.8);
	}

	ALBEDO = ground * mix(0.36, 1.0, seen) * (inside ? 1.0 : 0.7);
	ROUGHNESS = 1.0;

	// Fine relief: tilt the normal by the bumps' slope (the mesh has no tangents, so tilt it in world space and
	// carry that into view space).
	float e = 0.05;
	float h = bumps(p);
	float dx = (bumps(p + vec2(e, 0.0)) - h) / e;
	float dz = (bumps(p + vec2(0.0, e)) - h) / e;
	vec3 tilt = (VIEW_MATRIX * vec4(-dx * 0.12, 0.0, -dz * 0.12, 0.0)).xyz;
	NORMAL = normalize(NORMAL + tilt);
}
