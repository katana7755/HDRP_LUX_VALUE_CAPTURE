#ifndef LUX_TO_COLOR_LUT
#define LUX_TO_COLOR_LUT

struct LUTItem
{        
    float4 _Color;
    float _UpperLimit;
};

uint _LuxToColor_Count;
StructuredBuffer<LUTItem> _LuxToColor_Buffer;

float4 GetColorFromLux(float lux)
{
    int findIndex = _LuxToColor_Count - 1;

    for (int i = 0; i < _LuxToColor_Count; ++i)
    {
        if (lux < _LuxToColor_Buffer[i]._UpperLimit)
        {
            findIndex = i;
            break;
        }
    }

    return _LuxToColor_Buffer[findIndex]._Color;
}

#endif