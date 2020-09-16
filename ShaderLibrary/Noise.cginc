#ifndef NOISEUTILS
#define NOISEUTILS
//from https://www.shadertoy.com/view/4djSRW
// Hash without Sine
// MIT License...
/* Copyright (c)2014 David Hoskins.
Small modifications (GLSL to HLSL, renaming) by Sean Boettger

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/
//#define LookupNoise
#define IntHashNoise
#ifdef LookupNoise
Texture2D NoiseRGB16bit;

SamplerState my_point_repeat_sampler;
//  1 out, 1 in...
float hash(float p)
{
  return NoiseRGB16bit.SampleLevel(my_point_repeat_sampler, float2(p, p*294.5983) * 8.04, 0).r;
}
//  1 out, 2 in...
float hash(float2 p)
{
  return NoiseRGB16bit.SampleLevel(my_point_repeat_sampler, p * float2(7.5, 4.21875), 0).r;
}
//  2 out, 2 in...
float2 hash2(float2 p)
{
  return NoiseRGB16bit.SampleLevel(my_point_repeat_sampler, p * float2(7.5, 4.21875), 0).rg;
}
//  1 out, 3 in...
float hash(float3 p)
{
  return NoiseRGB16bit.SampleLevel(my_point_repeat_sampler, frac(p.z * float2(495.6934,294.69)) * float2(5.6,59.38) + p * 8.04, 0).r;
}
//  3 out, 2 in...
float hash3(float2 p)
{
  return NoiseRGB16bit.SampleLevel(my_point_repeat_sampler, p * 8.04, 0).rgb;
}
#else
#ifdef IntHashNoise
//Thank you so much Spatial! - https://stackoverflow.com/a/17479300

// A single iteration of Bob Jenkins' One-At-A-Time hashing algorithm.
uint hash(uint x) {
  x += (x << 10u);
  x ^= (x >> 6u);
  x += (x << 3u);
  x ^= (x >> 11u);
  x += (x << 15u);
  return x;
}



// Compound versions of the hashing algorithm I whipped together.
uint hash(uint2 v) { return hash(v.x ^ hash(v.y)); }
uint hash(uint3 v) { return hash(v.x ^ hash(v.y) ^ hash(v.z)); }
uint hash(uint4 v) { return hash(v.x ^ hash(v.y) ^ hash(v.z) ^ hash(v.w)); }



// Construct a float with half-open range [0:1] using low 23 bits.
// All zeroes yields 0.0, all ones yields the next smallest representable value below 1.0.
float floatConstruct(uint m) {
  uint ieeeMantissa = 0x007FFFFFu; // binary32 mantissa bitmask
  uint ieeeOne = 0x3F800000u; // 1.0 in IEEE binary32

  m &= ieeeMantissa;                     // Keep only mantissa bits (fractional part)
  m |= ieeeOne;                          // Add fractional part to 1.0

  float  f = asfloat(m);       // Range [1:2]
  return f - 1.0;                        // Range [0:1]
}



// Pseudo-random value in half-open range [0:1].
float hash(float x) { return floatConstruct(hash(asuint(x))); }
float hash(float2  v) { return floatConstruct(hash(asuint(v))); }
float2 hash2(float  v) { return float2(hash(v), hash(v + 10)); }
float3 hash3(float2  v) { return float3(hash(v), hash(v + 10), hash(v + 20)); }
float hash(float3  v) { return floatConstruct(hash(asuint(v))); }
float hash(float4  v) { return floatConstruct(hash(asuint(v))); }

#else
//----------------------------------------------------------------------------------------
//  1 out, 1 in...
float hash(float p)
{
  p = frac(p * .1031);
  p *= p + 33.33;
  p *= p + p;
  return frac(p);
}

//----------------------------------------------------------------------------------------
//  1 out, 2 in...
float hash(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * .1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  1 out, 3 in...
float hash(float3 p3)
{
  p3 = frac(p3 * .1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  2 out, 1 in...
float2 hash2(float p)
{
  float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 2 in...
float2 hash2(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 3 in...
float2 hash2(float3 p3)
{
  p3 = frac(p3 * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xx + p3.yz) * p3.zy);
}

//----------------------------------------------------------------------------------------
//  3 out, 1 in...
float3 hash3(float p)
{
  float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yzx + 33.33);
  return frac((p3.xxy + p3.yzz) * p3.zyx);
}


//----------------------------------------------------------------------------------------
///  3 out, 2 in...
float3 hash3(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yxz + 33.33);
  return frac((p3.xxy + p3.yzz) * p3.zyx);
}

//----------------------------------------------------------------------------------------
///  3 out, 3 in...
float3 hash3(float3 p3)
{
  p3 = frac(p3 * float3(.1031, .1030, .0973));
  p3 += dot(p3, p3.yxz + 33.33);
  return frac((p3.xxy + p3.yxx) * p3.zyx);

}

//----------------------------------------------------------------------------------------
// 4 out, 1 in...
float4 hash4(float p)
{
  float4 p4 = frac(float4(p, p, p, p) * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);

}

//----------------------------------------------------------------------------------------
// 4 out, 2 in...
float4 hash4(float2 p)
{
  float4 p4 = frac(float4(p.xyxy) * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);

}

//----------------------------------------------------------------------------------------
// 4 out, 3 in...
float4 hash4(float3 p)
{
  float4 p4 = frac(float4(p.xyzx) * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

//----------------------------------------------------------------------------------------
// 4 out, 4 in...
float4 hash4(float4 p4)
{
  p4 = frac(p4 * float4(.1031, .1030, .0973, .1099));
  p4 += dot(p4, p4.wzxy + 33.33);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

//-------------------------------------------------------------------
#endif
#endif



float stratify(float val, int count, int index)
{
  return 1.0/count * index + val / count;
}
#endif