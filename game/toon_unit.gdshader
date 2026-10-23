// The cel shaded look for the simple futuristic units (tools/art/model_hover.py; the user, 2026-10-23: "the shell
// shaded look we used in void wake"): flat baked colours under hard toon light bands, a thin rim of light, team
// colour where the texture's alpha marks paint, and the cyan and yellow lights shining. The ink outline is a second
// pass (toon_outline.gdshader).
shader_type spatial;
render_mode diffuse_toon, specular_toon;

uniform sampler2D skin : source_color, filter_linear_mipmap_anisotropic;
uniform vec3 team : source_color = vec3(0.27, 0.43, 0.75);
uniform float tone = 0.55;   // (the armour darker under the bright battlefield sun, as dark slate as its concept)

const vec3 BAKED_PAINT = vec3(0.275, 0.431, 0.745);

void fragment() {
	vec4 c = texture(skin, UV);
	bool paint = c.a < 0.5;
	float shade = clamp(c.b / BAKED_PAINT.b, 0.0, 1.2);
	vec3 col = paint ? team * shade : c.rgb;
	bool cyan = c.b > 0.8 && c.g > 0.7 && c.r < 0.55;
	bool yellow = c.r > 0.85 && c.g > 0.65 && c.b < 0.4;
	bool lit = !paint && (cyan || yellow);
	ALBEDO = lit || paint ? col : col * tone;
	EMISSION = lit ? col * 2.2 : vec3(0.0);
	ROUGHNESS = 0.7;
	SPECULAR = 0.25;
	RIM = 0.35;
	RIM_TINT = 0.4;
}
