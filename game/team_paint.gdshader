// A unit's baked texture with its team colour put in: the texture's alpha is 0 where the model is team paint
// (tools/art/unit_parts.py "paint"), and that paint was baked blue with its shade, which is kept.
// The lit strips and lenses ("glow", cyan) and the ore ("green") shine: they're the only colours in the kit whose
// green or blue is well over their red and that are bright, so they're picked out by that.
shader_type spatial;

uniform sampler2D skin : source_color, filter_linear_mipmap;
uniform vec3 team : source_color = vec3(0.27, 0.43, 0.75);

const vec3 BAKED_PAINT = vec3(0.275, 0.431, 0.745);   // "paint" as the models baked it

void fragment() {
	vec4 c = texture(skin, UV);
	float shade = clamp(c.b / BAKED_PAINT.b, 0.0, 1.2);
	bool paint = c.a < 0.5;
	ALBEDO = paint ? team * shade : c.rgb;
	float bright = max(c.r, max(c.g, c.b));
	bool lit = !paint && bright > 0.5 && (c.g > c.r * 1.8 || c.b > c.r * 1.8);
	EMISSION = lit ? c.rgb * 1.8 : vec3(0.0);
	ROUGHNESS = paint ? 0.6 : 0.55;
	METALLIC = paint ? 0.1 : 0.35;
}
