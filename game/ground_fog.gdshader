// The battlefield's ground, darkened where the local player's side can't see (fog of war). `fog` has FogRes texels
// per world unit across the map, white where seen; Main fills it every tick. Past the map's edge it's darker still.
//
// Painted per pixel (the user, 2026-10-20: "higher definition battlefields"): dry earth everywhere, with broad patches
// paler and darker; scrub grass where the mesh says it's grassy (COLOR.b, game/Terrain.cs Grassy) with blades in it;
// dark damp earth by the ponds (COLOR.g); bare banded rock on steep slopes and high ground; gravel and pebbles; and the
// hollows shaded (COLOR.r, Terrain.Hollow). Small bumps tilt the light. All from world-space noise, so it never tiles.
shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D fog : filter_linear, repeat_disable;
uniform vec2 half_size = vec2(60.0, 40.0);

varying vec3 world;
varying vec3 world_normal;

// A random 0..1 for a whole-number lattice point (integer maths: neighbouring cells agree exactly on shared corners).
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
	for (int i = 0; i < 5; i++) { s += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
	return s;
}


// Cell edges (Voronoi: the gap between the nearest and second-nearest random point): 0 on an edge. Dried mud cracks.
float cell_edge(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	float d1 = 8.0, d2 = 8.0;
	for (int y = -1; y <= 1; y++)
		for (int x = -1; x <= 1; x++) {
			vec2 o = vec2(float(x), float(y));
			vec2 pt = o + vec2(hash(i + o), hash(i + o + vec2(37.0, 11.0)));
			float d = length(pt - f);
			if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
		}
	return d2 - d1;
}
// The fine relief the light catches: grains, clods, cracks.
float bumps(vec2 p) {
	return noise(p * 2.2) * 0.5 + noise(p * 6.5) * 0.3 + noise(p * 17.0) * 0.2;
}

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = (world.xz + half_size) / (half_size * 2.0);
	bool inside = uv.x >= 0.0 && uv.y >= 0.0 && uv.x <= 1.0 && uv.y <= 1.0;
	float seen = inside ? texture(fog, uv).r : 0.0;
	vec2 p = world.xz;
	float hollow = COLOR.r, wet = COLOR.g, grassy = COLOR.b;
	float slope = 1.0 - clamp(world_normal.y, 0.0, 1.0);   // 0 flat, more as it steepens

	// Dry earth, its tone drifting over tens of metres and mottled at a metre or so.
	vec3 earth_light = vec3(0.30, 0.25, 0.18), earth_dark = vec3(0.17, 0.14, 0.10);
	float broad = fbm(p * 0.035);
	vec3 col = mix(earth_dark, earth_light, smoothstep(0.3, 0.7, broad));
	col *= 0.85 + 0.3 * noise(p * 0.9);
	// Fine soil grain and specks: crisp detail for close up.
	col *= 0.82 + 0.36 * noise(p * 11.0);
	col *= 1.0 - step(0.93, hash(floor(p * 22.0))) * 0.35;
	// Dried mud: crack lines in patches of the open earth (not in the grass, not on wet ground).
	float cracked = smoothstep(0.55, 0.7, fbm(p * 0.12 + 40.0)) * (1.0 - wet);
	float crack_line = smoothstep(0.06, 0.0, cell_edge(p * 1.6));
	col *= 1.0 - crack_line * cracked * 0.45;

	// Scrub: olive grass in the grassy places, broken up at its edges, with streaks of blades.
	float grass_edge = smoothstep(0.15, 0.6, grassy + (fbm(p * 0.6) - 0.5) * 0.5);
	vec3 grass_a = vec3(0.10, 0.13, 0.05), grass_b = vec3(0.17, 0.18, 0.08);
	float blades = noise(vec2(p.x * 14.0, p.y * 3.5)) * 0.5 + noise(vec2(p.x * 4.0, p.y * 16.0)) * 0.5;
	vec3 grass = mix(grass_a, grass_b, blades) * (0.8 + 0.4 * noise(p * 1.7));
	col = mix(col, grass, grass_edge * (1.0 - slope * 2.0));

	// Damp earth by the ponds: darker, a little glossy.
	col = mix(col, col * 0.55 + vec3(0.0, 0.01, 0.01), wet * 0.85);

	// Rock on the steep slopes and the heights: grey, in bands that follow the height, cracked.
	float rocky = smoothstep(0.18, 0.4, slope + (noise(p * 0.5) - 0.5) * 0.15);   // (by steepness only: hilltops stay earth and grass)
	float strata = 0.5 + 0.5 * sin(world.y * 7.0 + noise(p * 0.8) * 4.0);
	vec3 rock = mix(vec3(0.24, 0.23, 0.21), vec3(0.38, 0.36, 0.33), strata) * (0.8 + 0.4 * noise(p * 3.0));
	float crack = smoothstep(0.04, 0.0, abs(noise(p * 1.4) - 0.5));
	rock *= 1.0 - crack * 0.5;
	col = mix(col, rock, clamp(rocky, 0.0, 1.0));

	// Gravel and pebbles on the open earth: round stones of random size in a grid of cells.
	vec2 cell = floor(p * 3.0), in_cell = fract(p * 3.0);
	float pick = hash(cell);
	if (pick < 0.16) {
		vec2 centre = vec2(hash(cell + vec2(7, 1)), hash(cell + vec2(3, 11))) * 0.6 + 0.2;
		float radius = 0.07 + 0.13 * hash(cell + vec2(19, 5));
		float pebble = smoothstep(radius, radius * 0.55, length(in_cell - centre)) * (1.0 - grass_edge * 0.8);
		vec3 stone = pick < 0.07 ? vec3(0.36, 0.35, 0.32) : col * 0.5;
		float lit = 0.8 + 0.4 * clamp(centre.y - in_cell.y + 0.5, 0.0, 1.0);   // lit on one side
		col = mix(col, stone * lit, pebble * 0.85);
	}

	// Hollows darker, ridges a touch lighter.
	col *= mix(1.15, 0.6, hollow);

	ALBEDO = col * mix(0.32, 1.0, seen) * (inside ? 1.0 : 0.65);
	ROUGHNESS = mix(0.95, 0.55, wet);
	SPECULAR = mix(0.25, 0.5, wet);

	// The light catches the small relief (world-space tilt, carried into view space: the mesh has no tangents).
	float e = 0.04;
	float h = bumps(p);
	float dx = (bumps(p + vec2(e, 0.0)) - h) / e;
	float dz = (bumps(p + vec2(0.0, e)) - h) / e;
	float strength = 0.10 + rocky * 0.12;
	vec3 tilt = (VIEW_MATRIX * vec4(-dx * strength, 0.0, -dz * strength, 0.0)).xyz;
	NORMAL = normalize(NORMAL + tilt);
}
