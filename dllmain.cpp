// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "Noise/Simplex.h"
//#include <iostream>

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

#define FASTFLOOR(x) ( ((x)>0) ? ((int)x) : (((int)x)-1) )
#define F2 0.366025403f
#define G2 0.211324865f
/* Skewing factors for 3D simplex grid:
 * F3 = 1/3
 * G3 = 1/6 */
#define F3 0.333333333f
#define G3 0.166666667f

 // The skewing and unskewing factors are hairy again for the 4D case
#define F4 0.309016994f // F4 = (Math.sqrt(5.0)-1.0)/4.0
#define G4 0.138196601f // G4 = (5.0-Math.sqrt(5.0))/20.0

float betterNoise2(glm::vec3 input, glm::vec3& normal);
float x_squared_clamp(float input, float point_9);
int IntPow(int value, int power);
float process_for_derivitive(glm::vec3 normal);

namespace details {
	/*
	 * Permutation table. This is just a random jumble of all numbers 0-255,
	 * repeated twice to avoid wrapping the index at 255 for each lookup.
	 * This needs to be exactly the same for all instances on all platforms,
	 * so it's easiest to just keep it as static explicit data.
	 * This also removes the need for any initialisation of this class.
	 *
	 * Note that making this an int[] instead of a char[] might make the
	 * code run faster on platforms with a high penalty for unaligned single
	 * byte addressing. Intel x86 is generally single-byte-friendly, but
	 * some other CPUs are faster with 4-aligned reads.
	 * However, a char[] is smaller, which avoids cache trashing, and that
	 * is probably the most important aspect on most architectures.
	 * This array is accessed a *lot* by the noise functions.
	 * A vector-valued noise over 3D accesses it 96 times, and a
	 * float-valued 4D noise 64 times. We want this to fit in the cache!
	 */
#ifdef SIMPLEX_INTEGER_LUTS
	typedef uint8_t LutType;
#else
	typedef unsigned char LutType;
#endif

	static LutType perm[512] = { 151,160,137,91,90,15,
		131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
		190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
		88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
		77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
		102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
		135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
		5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
		223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
		129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
		251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
		49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
		138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
		151,160,137,91,90,15,
		131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
		190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
		88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
		77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
		102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
		135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
		5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
		223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
		129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
		251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
		49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
		138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
	};

	/*
	 * Gradient tables. These could be programmed the Ken Perlin way with
	 * some clever bit-twiddling, but this is more clear, and not really slower.
	 */
	static float grad2lut[8][2] = {
		{ -1.0f, -1.0f }, { 1.0f, 0.0f } , { -1.0f, 0.0f } , { 1.0f, 1.0f } ,
		{ -1.0f, 1.0f } , { 0.0f, -1.0f } , { 0.0f, 1.0f } , { 1.0f, -1.0f }
	};

	/*
	 * Gradient directions for 3D.
	 * These vectors are based on the midpoints of the 12 edges of a cube.
	 * A larger array of random unit length vectors would also do the job,
	 * but these 12 (including 4 repeats to make the array length a power
	 * of two) work better. They are not random, they are carefully chosen
	 * to represent a small, isotropic set of directions.
	 */

	static float grad3lut[16][3] = {
		{ 1.0f, 0.0f, 1.0f }, { 0.0f, 1.0f, 1.0f }, // 12 cube edges
		{ -1.0f, 0.0f, 1.0f }, { 0.0f, -1.0f, 1.0f },
		{ 1.0f, 0.0f, -1.0f }, { 0.0f, 1.0f, -1.0f },
		{ -1.0f, 0.0f, -1.0f }, { 0.0f, -1.0f, -1.0f },
		{ 1.0f, -1.0f, 0.0f }, { 1.0f, 1.0f, 0.0f },
		{ -1.0f, 1.0f, 0.0f }, { -1.0f, -1.0f, 0.0f },
		{ 1.0f, 0.0f, 1.0f }, { -1.0f, 0.0f, 1.0f }, // 4 repeats to make 16
		{ 0.0f, 1.0f, -1.0f }, { 0.0f, -1.0f, -1.0f }
	};

	static float grad4lut[32][4] = {
		{ 0.0f, 1.0f, 1.0f, 1.0f }, { 0.0f, 1.0f, 1.0f, -1.0f }, { 0.0f, 1.0f, -1.0f, 1.0f }, { 0.0f, 1.0f, -1.0f, -1.0f }, // 32 tesseract edges
		{ 0.0f, -1.0f, 1.0f, 1.0f }, { 0.0f, -1.0f, 1.0f, -1.0f }, { 0.0f, -1.0f, -1.0f, 1.0f }, { 0.0f, -1.0f, -1.0f, -1.0f },
		{ 1.0f, 0.0f, 1.0f, 1.0f }, { 1.0f, 0.0f, 1.0f, -1.0f }, { 1.0f, 0.0f, -1.0f, 1.0f }, { 1.0f, 0.0f, -1.0f, -1.0f },
		{ -1.0f, 0.0f, 1.0f, 1.0f }, { -1.0f, 0.0f, 1.0f, -1.0f }, { -1.0f, 0.0f, -1.0f, 1.0f }, { -1.0f, 0.0f, -1.0f, -1.0f },
		{ 1.0f, 1.0f, 0.0f, 1.0f }, { 1.0f, 1.0f, 0.0f, -1.0f }, { 1.0f, -1.0f, 0.0f, 1.0f }, { 1.0f, -1.0f, 0.0f, -1.0f },
		{ -1.0f, 1.0f, 0.0f, 1.0f }, { -1.0f, 1.0f, 0.0f, -1.0f }, { -1.0f, -1.0f, 0.0f, 1.0f }, { -1.0f, -1.0f, 0.0f, -1.0f },
		{ 1.0f, 1.0f, 1.0f, 0.0f }, { 1.0f, 1.0f, -1.0f, 0.0f }, { 1.0f, -1.0f, 1.0f, 0.0f }, { 1.0f, -1.0f, -1.0f, 0.0f },
		{ -1.0f, 1.0f, 1.0f, 0.0f }, { -1.0f, 1.0f, -1.0f, 0.0f }, { -1.0f, -1.0f, 1.0f, 0.0f }, { -1.0f, -1.0f, -1.0f, 0.0f }
	};

	/*
	 * For 3D, we define two orthogonal vectors in the desired rotation plane.
	 * These vectors are based on the midpoints of the 12 edges of a cube,
	 * they all rotate in their own plane and are never coincident or collinear.
	 * A larger array of random vectors would also do the job, but these 12
	 * (including 4 repeats to make the array length a power of two) work better.
	 * They are not random, they are carefully chosen to represent a small
	 * isotropic set of directions for any rotation angle.
	 */

	 /* a = sqrt(2)/sqrt(3) = 0.816496580 */
#define a 0.81649658f

	static float grad3u[16][3] = {
  { 1.0f, 0.0f, 1.0f }, { 0.0f, 1.0f, 1.0f }, // 12 cube edges
  { -1.0f, 0.0f, 1.0f }, { 0.0f, -1.0f, 1.0f },
  { 1.0f, 0.0f, -1.0f }, { 0.0f, 1.0f, -1.0f },
  { -1.0f, 0.0f, -1.0f }, { 0.0f, -1.0f, -1.0f },
  { a, a, a }, { -a, a, -a },
  { -a, -a, a }, { a, -a, -a },
  { -a, a, a }, { a, -a, a },
  { a, -a, -a }, { -a, a, -a }
	};

	static float grad3v[16][3] = {
  { -a, a, a }, { -a, -a, a },
  { a, -a, a }, { a, a, a },
  { -a, -a, -a }, { a, -a, -a },
  { a, a, -a }, { -a, a, -a },
  { 1.0f, -1.0f, 0.0f }, { 1.0f, 1.0f, 0.0f },
  { -1.0f, 1.0f, 0.0f }, { -1.0f, -1.0f, 0.0f },
  { 1.0f, 0.0f, 1.0f }, { -1.0f, 0.0f, 1.0f }, // 4 repeats to make 16
  { 0.0f, 1.0f, -1.0f }, { 0.0f, -1.0f, -1.0f }
	};

#undef a


	//---------------------------------------------------------------------

	/*
	 * Helper functions to compute gradients-dot-residualvectors (1D to 4D)
	 * Note that these generate gradients of more than unit length. To make
	 * a close match with the value range of classic Perlin noise, the final
	 * noise values need to be rescaled to fit nicely within [-1,1].
	 * (The simplex noise functions as such also have different scaling.)
	 * Note also that these noise functions are the most practical and useful
	 * signed version of Perlin noise. To return values according to the
	 * RenderMan specification from the SL noise() and pnoise() functions,
	 * the noise values need to be scaled and offset to [0,1], like this:
	 * float SLnoise = (SimplexNoise1234::noise(x,y,z) + 1.0) * 0.5;
	 */

	inline float  grad(int hash, float x) {
		int h = hash & 15;
		float grad = 1.0f + (h & 7);   // Gradient value 1.0, 2.0, ..., 8.0
		if (h & 8) grad = -grad;         // Set a random sign for the gradient
		return (grad * x);           // Multiply the gradient with the distance
	}

	inline float  grad(int hash, float x, float y) {
		int h = hash & 7;      // Convert low 3 bits of hash code
		float u = h < 4 ? x : y;  // into 8 simple gradient directions,
		float v = h < 4 ? y : x;  // and compute the dot product with (x,y).
		return ((h & 1) ? -u : u) + ((h & 2) ? -2.0f * v : 2.0f * v);
	}

	inline float  grad(int hash, float x, float y, float z) {
		int h = hash & 15;     // Convert low 4 bits of hash code into 12 simple
		float u = h < 8 ? x : y; // gradient directions, and compute dot product.
		float v = h < 4 ? y : h == 12 || h == 14 ? x : z; // Fix repeats at h = 12 to 15
		return ((h & 1) ? -u : u) + ((h & 2) ? -v : v);
	}

	inline float  grad(int hash, float x, float y, float z, float t) {
		int h = hash & 31;      // Convert low 5 bits of hash code into 32 simple
		float u = h < 24 ? x : y; // gradient directions, and compute dot product.
		float v = h < 16 ? y : z;
		float w = h < 8 ? z : t;
		return ((h & 1) ? -u : u) + ((h & 2) ? -v : v) + ((h & 4) ? -w : w);
	}

	/*
	 * Helper functions to compute gradients in 1D to 4D
	 * and gradients-dot-residualvectors in 2D to 4D.
	 */
	inline void grad1(int hash, float* gx) {
		int h = hash & 15;
		*gx = 1.0f + (h & 7);   // Gradient value is one of 1.0, 2.0, ..., 8.0
		if (h & 8) *gx = -*gx;   // Make half of the gradients negative
	}

	inline void grad2(int hash, float* gx, float* gy) {
		int h = hash & 7;
		*gx = grad2lut[h][0];
		*gy = grad2lut[h][1];
		return;
	}

	inline void grad3(int hash, float* gx, float* gy, float* gz) {
		int h = hash & 15;
		*gx = grad3lut[h][0];
		*gy = grad3lut[h][1];
		*gz = grad3lut[h][2];
		return;
	}

	inline void grad4(int hash, float* gx, float* gy, float* gz, float* gw) {
		int h = hash & 31;
		*gx = grad4lut[h][0];
		*gy = grad4lut[h][1];
		*gz = grad4lut[h][2];
		*gw = grad4lut[h][3];
		return;
	}


	/*
	 * Helper functions to compute rotated gradients and
	 * gradients-dot-residualvectors in 2D and 3D.
	 */

	inline void gradrot2(int hash, float sin_t, float cos_t, float* gx, float* gy) {
		int h = hash & 7;
		float gx0 = grad2lut[h][0];
		float gy0 = grad2lut[h][1];
		*gx = cos_t * gx0 - sin_t * gy0;
		*gy = sin_t * gx0 + cos_t * gy0;
		return;
	}

	inline void gradrot3(int hash, float sin_t, float cos_t, float* gx, float* gy, float* gz) {
		int h = hash & 15;
		float gux = grad3u[h][0];
		float guy = grad3u[h][1];
		float guz = grad3u[h][2];
		float gvx = grad3v[h][0];
		float gvy = grad3v[h][1];
		float gvz = grad3v[h][2];
		*gx = cos_t * gux + sin_t * gvx;
		*gy = cos_t * guy + sin_t * gvy;
		*gz = cos_t * guz + sin_t * gvz;
		return;
	}

	inline float graddotp2(float gx, float gy, float x, float y) {
		return gx * x + gy * y;
	}

	inline float graddotp3(float gx, float gy, float gz, float x, float y, float z) {
		return gx * x + gy * y + gz * z;
	}
}

class infoClass 
{
public:
	glm::vec2 layer_rotation[20];
	int layer_length;
	float gradient_limit;
	float threshold;
	float start_magnitude;
	float start_frequency;
	float gradient_power;



};

extern "C"
{
	infoClass inf = infoClass();
	float DLL_EXPORT alternativeNoise(float inx, float iny, float inz, float& outx, float& outy, float& outz) {
		glm::vec3 input = glm::vec3(inx, iny, inz);
		glm::vec4 output = Simplex::dnoise(input);
		outx = output.y;
		outy = output.z;
		outz = output.w;
		return output.x;
	}
	void DLL_EXPORT setInfo(float layer_rotation_x[20], float layer_rotation_y[20], int length, float gradLimit, float thresh, float startMagnitude, float startFrequency, float gradPower)
	{
		for (int i = 0; i < 20; i++)
		{
			inf.layer_rotation[i].x = layer_rotation_x[i];
			inf.layer_rotation[i].y = layer_rotation_y[i];
		}
		inf.gradient_limit = gradLimit;
		inf.layer_length = length;
		inf.threshold = thresh;
		inf.start_magnitude = startMagnitude;
		inf.start_frequency = startFrequency;
		inf.gradient_power = inf.gradient_power;
	}


	float DLL_EXPORT octaveNoise2(float inputx, float inputy, float inputz, float top_stretchx, float top_stretchy, float top_stretchz, int octaves, float start)
	{
		//std::cout << inf.gradient_power << "\n";
		glm::vec3 input = glm::vec3(inputx, inputy, inputz);
		glm::vec3 top_stretch = glm::vec3(top_stretchx, top_stretchy, top_stretchz);
		float value = start;

		glm::vec3 store_offset = glm::vec3();

		float x = input.x * inf.layer_rotation[0].x - input.z * inf.layer_rotation[0].y;
		float z = input.x * inf.layer_rotation[0].y + input.z * inf.layer_rotation[0].x;

		glm::vec3 hold = glm::vec3((x)*top_stretch.x, (input.y) * top_stretch.y, (z)*top_stretch.z) * inf.start_frequency;
		glm::vec3 gradient;
		value += betterNoise2(hold, gradient) * inf.start_magnitude;

		float cumulitive_damening = x_squared_clamp(process_for_derivitive(gradient), inf.gradient_limit);

		if (value >= inf.threshold - inf.start_magnitude/* && value < threshold*/)
		{
			for (int i = 1; i < octaves; i++)
			{
				x = input.x * inf.layer_rotation[i].x - input.z * inf.layer_rotation[i].y;
				z = input.x * inf.layer_rotation[i].y + input.z * inf.layer_rotation[i].x;
				float str = IntPow(2, i) * inf.start_frequency;
				hold = glm::vec3((x)*top_stretch.x * str, (input.y) * top_stretch.y * str, (z)*top_stretch.z * str);

				value += betterNoise2(hold, gradient) * glm::pow(2, -1 * i) * inf.start_magnitude / glm::pow(1 + cumulitive_damening, inf.gradient_power);
				cumulitive_damening += x_squared_clamp(process_for_derivitive(gradient), inf.gradient_limit);
				if (value < inf.threshold - (inf.start_magnitude * glm::pow(2, -1 * i)))
				{
					break;
				}
			}
		}

		return value;
	}
}

float process_for_derivitive(glm::vec3 normal)
{
	return glm::abs(glm::sqrt(normal.x * normal.x + normal.z * normal.z)) / normal.y;
}


int IntPow(int value, int power)
{
	int to_return = 1;
	for (int i = 0; i < power; i++)
	{
		to_return *= value;
	}
	return to_return;
}

float betterNoise2(glm::vec3 input, glm::vec3 &normal)
{
	float outx = 0;
	float outy = 0;
	float outz = 0;
	float to_return = alternativeNoise(input.x, input.y, input.z, outx, outy, outz);

	normal = glm::vec3(outx, outy, outz);
	return to_return;
}

float x_squared_clamp(float input, float point_9)
{
	if (glm::isnan(input) || glm::isinf(input))
	{
		return point_9;
	}
	float v = (input) / glm::sqrt(((input / point_9) * (input / point_9)) + 1);
	return v;
}



