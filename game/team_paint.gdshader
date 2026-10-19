// A unit's baked texture with its team colour put in: the texture's alpha is 0 where the model is team paint
// (tools/art/model_units.py "paint"), and that paint was baked blue with its shade, which is kept.
shader_type spatial;

uniform sampler2D skin : source_color, filter_linear_mipmap;
uniform vec3 team : source_color = vec3(0.27, 0.43, 0.75);

const vec3 BAKED_PAINT = vec3(0.275, 0.431, 0.745);   // "paint" as model_units.py baked it

void fragment() {
	vec4 c = texture(skin, UV);
	float shade = clamp(c.b / BAKED_PAINT.b, 0.0, 1.2);
	ALBEDO = c.a < 0.5 ? team * shade : c.rgb;
	ROUGHNESS = 0.85;
}
