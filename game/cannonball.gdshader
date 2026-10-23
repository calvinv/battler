// The battle wheel's cannonball: dark iron, banded with two hot seams and studded with glowing rivets, the heat
// flickering. The pattern is in the ball's own space, so it turns as the ball spins. INSTANCE colour = the heat colour.
shader_type spatial;
render_mode diffuse_toon, specular_toon;

varying vec3 obj;
varying vec4 heat;
varying float seed;

void vertex() {
	obj = normalize(VERTEX);
	heat = COLOR;
	seed = float(INSTANCE_ID) * 1.37;
}

void fragment() {
	vec3 p = obj;
	// Two seams round the ball, at right angles, and eight rivets between them.
	float seam = min(abs(p.y), abs(p.x));
	float glow = 1.0 - smoothstep(0.035, 0.09, seam);
	vec3 q = abs(p);
	float rivet = 1.0 - smoothstep(0.07, 0.11, length(q - vec3(0.577)));
	glow = max(glow, rivet * 0.8);
	// The heat flickers, and the side facing the camera's edge glows (hot air round it).
	float flicker = 0.75 + 0.25 * sin(TIME * 31.0 + seed) * sin(TIME * 17.0 + seed * 2.1);
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	ALBEDO = vec3(0.10, 0.095, 0.09);
	ROUGHNESS = 0.45;
	METALLIC = 0.6;
	EMISSION = heat.rgb * (glow * 1.6 * flicker + rim * 0.7);
}
