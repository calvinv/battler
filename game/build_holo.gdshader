// A unit being built at its factory's door, "digitising up" (the user, 2026-10-19: "add the visual of building a
// tank, even if it's digitising up"): below the build line it's solid (its skin, team paint put in, as
// team_paint.gdshader), above it a faint hologram with scanlines, and a bright line where the two meet. `progress`
// (0 to 1) is set per ghost; `height` is the model's height in its own units.
shader_type spatial;
render_mode cull_disabled, specular_disabled;

uniform sampler2D skin : source_color, filter_linear_mipmap;
uniform vec3 team : source_color = vec3(0.27, 0.43, 0.75);
uniform float height = 12.0;
instance uniform float progress = 0.0;

const vec3 BAKED_PAINT = vec3(0.275, 0.431, 0.745);

varying float up;   // height above the model's foot, 0 to 1

void vertex() {
	up = VERTEX.y / height;
}

void fragment() {
	float line = progress * 1.02;   // (a hair past the top, so a finished one is all solid)
	if (up <= line) {
		vec4 c = texture(skin, UV);
		float shade = clamp(c.b / BAKED_PAINT.b, 0.0, 1.2);
		ALBEDO = c.a < 0.5 ? team * shade : c.rgb;
		float edge = smoothstep(0.035, 0.0, line - up);   // the fresh layer glows
		EMISSION = mix(team, vec3(1.0), 0.6) * edge * 2.5;
		ALPHA = 1.0;
	} else {
		float scan = 0.5 + 0.5 * sin(up * 90.0 - TIME * 6.0);
		ALBEDO = vec3(0.0);
		EMISSION = mix(team, vec3(0.6, 0.95, 1.0), 0.5) * (0.6 + 0.8 * scan);
		ALPHA = 0.12 + 0.18 * scan;
	}
}
