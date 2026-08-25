$input v_wpos
/*
 * Project Riftward - Schattenpass-Fragmentshader der Graybox-Benchmarkszene
 * (T-023): speichert die Distanz zur Lichtposition in allen Kanälen.
 */


void main()
{
	gl_FragColor = vec4(length(v_wpos), length(v_wpos), length(v_wpos), 1.0);
}
