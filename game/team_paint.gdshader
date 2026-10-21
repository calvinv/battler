// A unit's baked texture with its team colour put in: the texture's alpha is 0 where the model is team paint
// (tools/art/unit_parts.py "paint"), and that paint was baked blue with its shade, which is kept.
// The lit strips and lenses ("glow", cyan) and the ore ("green") shine: they're the only colours in the kit whose
// green or blue is well over their red and that are bright, so they're picked out by that.
shader_type spatial;

uniform sampler2D skin : source_color, filter_linear_mipmap;
uniform vec3 team : source_color = vec3(0.27, 0.43, 0.75);

const vec3 BAKED_PAINT = vec3(0.275, 0.431, 0.745);   // "paint" as the models baked it

// Fine surface grain in the model's own space (it moves with the model), so close up it isn't flat plastic
// (the user, 2026-10-21: "texture these models so they look realistic"): a little roughness and shade variation and
// a faint bumpiness that catches the light.
varying vec3 local;

float hash3(vec3 p) {
	p = fract(p * 0.3183099 + 0.1);
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise3(vec3 p) {
	vec3 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
	           mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

void vertex() {
	local = VERTEX;
}

void fragment() {
	vec4 c = texture(skin, UV);
	vec3 p = local * 3.0;   // (models are ~0.18 units a kit unit: this is a few features a metre)
	float grain = noise3(p * 4.0) * 0.6 + noise3(p * 13.0) * 0.4;
	float shade = clamp(c.b / BAKED_PAINT.b, 0.0, 1.2);
	bool paint = c.a < 0.5;
	ALBEDO = paint ? team * shade : c.rgb;
	float bright = max(c.r, max(c.g, c.b));
	bool amber = c.r > 0.95 && c.g < 0.75 && c.b < 0.3;   // the Lattice's eyes and cores
	bool lit = !paint && ((bright > 0.5 && (c.g > c.r * 1.8 || c.b > c.r * 1.8)) || amber);
	EMISSION = lit ? c.rgb * 1.8 : vec3(0.0);
	if (!lit) ALBEDO *= 0.94 + 0.12 * grain;
	ROUGHNESS = (paint ? 0.62 : 0.52) + (grain - 0.5) * 0.3;
	METALLIC = paint ? 0.1 : 0.35;
	// The grain tilts the light a touch (its slope, in model space, carried into view space).
	float e = 0.05;
	vec3 g = vec3(noise3((p + vec3(e, 0, 0)) * 13.0), noise3((p + vec3(0, e, 0)) * 13.0), noise3((p + vec3(0, 0, e)) * 13.0)) - noise3(p * 13.0);
	vec3 tilt = (VIEW_MATRIX * MODEL_MATRIX * vec4(g * 0.6, 0.0)).xyz;
	NORMAL = normalize(NORMAL - tilt * (lit ? 0.0 : 1.0));
}
