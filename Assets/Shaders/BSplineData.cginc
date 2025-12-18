// Link Type 
#define STRAIGHT_LINK 0
#define BUNDLED_LINK 1

// Structs for Data
struct SplineData {
    uint Idx;
	uint NumSegments;
	uint BeginSplineSegmentIdx;
	uint NumSamples;
	uint BeginSamplePointIdx;
	float3 StartPosition;
	float3 EndPosition;
	float4 StartColorRGBA;
	float4 EndColorRGBA;
	uint LinkType;
};

struct SplineSegmentData {
    uint SplineIdx;
	uint BeginControlPointIdx;
    uint BeginSamplePointIdx;
	uint NumSamples;
};

struct SplineControlPointData {
    float3 Position;
};

struct SplineSamplePointData {
	float3 Position;
	float4 ColorRGBA;
	uint SplineIdx;
};

// Data Buffers
StructuredBuffer<SplineData> InSplineData;
StructuredBuffer<SplineSegmentData> InSplineSegmentData;
StructuredBuffer<SplineControlPointData> InSplineControlPointData;

#ifdef SHADER_CODE
	StructuredBuffer<SplineSamplePointData> OutSamplePointData;
#else
	RWStructuredBuffer<SplineSamplePointData> OutSamplePointData;
#endif