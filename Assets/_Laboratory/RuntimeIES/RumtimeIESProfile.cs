#define IES_ENGINE_DEFINITION

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.Collections;

public static class RumtimeIESProfile
{
    /// <summary>
    /// Class to Parse IES File
    /// </summary>
    [System.Serializable]
    public class IESReader
    {
        string m_FileFormatVersion;
        /// <summary>
        /// Version of the IES File
        /// </summary>
        public string FileFormatVersion
        {
            get { return m_FileFormatVersion; }
        }

        float m_TotalLumens;
        /// <summary>
        /// Total light intensity (in Lumens) stored on the file, usage of it is optional (through the prefab subasset inside the IESObject)
        /// </summary>
        public float TotalLumens
        {
            get { return m_TotalLumens; }
        }

        float m_MaxCandelas;
        /// <summary>
        /// Maximum of Candela in the IES File
        /// </summary>
        public float MaxCandelas
        {
            get { return m_MaxCandelas; }
        }

        int m_PhotometricType;

        /// <summary>
        /// Type of Photometric light in the IES file, varying per IES-Type and version
        /// </summary>
        public int PhotometricType
        {
            get { return m_PhotometricType; }
        }

        // JHLEE ++++++++++
        float m_DimensionWidth;

        public float DimensionWidth
        {            
            get { return m_DimensionWidth; }
        }

        float m_DimensionLength;

        public float DimensionLength
        {            
            get { return m_DimensionLength; }
        }

        float m_DimensionHeight;

        public float DimensionHeight
        {
            get { return m_DimensionHeight; }
        }
        // ++++++++++

        Dictionary<string, string> m_KeywordDictionary = new Dictionary<string, string>();

        int m_VerticalAngleCount;
        int m_HorizontalAngleCount;
        float[] m_VerticalAngles;
        float[] m_HorizontalAngles;
        float[] m_CandelaValues;

        float m_MinDeltaVerticalAngle;
        float m_MinDeltaHorizontalAngle;
        float m_FirstHorizontalAngle;
        float m_LastHorizontalAngle;

        // File format references:
        // https://www.ies.org/product/standard-file-format-for-electronic-transfer-of-photometric-data/
        // http://lumen.iee.put.poznan.pl/kw/iesna.txt
        // https://seblagarde.wordpress.com/2014/11/05/ies-light-format-specification-and-reader/
        /// <summary>
        /// Main function to read the file
        /// </summary>
        /// <param name="iesFilePath">The path to the IES File on disk.</param>
        /// <returns>Return the error during the import otherwise null if no error</returns>
        public string ReadFile(string iesFilePath)
        {

            using (var iesReader = File.OpenText(iesFilePath))
            {
                string versionLine = iesReader.ReadLine();

                if (versionLine == null)
                {
                    return "Premature end of file (empty file).";
                }

                switch (versionLine.Trim())
                {
                    case "IESNA91":
                        m_FileFormatVersion = "LM-63-1991";
                        break;
                    case "IESNA:LM-63-1995":
                        m_FileFormatVersion = "LM-63-1995";
                        break;
                    case "IESNA:LM-63-2002":
                        m_FileFormatVersion = "LM-63-2002";
                        break;
                    case "IES:LM-63-2019":
                        m_FileFormatVersion = "LM-63-2019";
                        break;
                    default:
                        m_FileFormatVersion = "LM-63-1986";
                        break;
                }

                var keywordRegex = new Regex(@"\s*\[(?<keyword>\w+)\]\s*(?<data>.*)", RegexOptions.Compiled);
                var tiltRegex = new Regex(@"TILT=(?<data>.*)", RegexOptions.Compiled);

                string currentKeyword = string.Empty;

                for (string keywordLine = (m_FileFormatVersion == "LM-63-1986") ? versionLine : iesReader.ReadLine(); true; keywordLine = iesReader.ReadLine())
                {
                    if (keywordLine == null)
                    {
                        return "Premature end of file (missing TILT=NONE).";
                    }

                    if (string.IsNullOrWhiteSpace(keywordLine))
                    {
                        continue;
                    }

                    Match keywordMatch = keywordRegex.Match(keywordLine);

                    if (keywordMatch.Success)
                    {
                        string keyword = keywordMatch.Groups["keyword"].Value;
                        string data = keywordMatch.Groups["data"].Value.Trim();

                        if (keyword == currentKeyword || keyword == "MORE")
                        {
                            m_KeywordDictionary[currentKeyword] += $" {data}";
                        }
                        else
                        {
                            // Many separate occurrences of keyword OTHER will need to be handled properly once exposed in the inspector.
                            currentKeyword = keyword;
                            m_KeywordDictionary[currentKeyword] = data;
                        }

                        continue;
                    }

                    Match tiltMatch = tiltRegex.Match(keywordLine);

                    if (tiltMatch.Success)
                    {
                        string data = tiltMatch.Groups["data"].Value.Trim();

                        if (data == "NONE")
                        {
                            break;
                        }

                        return $"TILT format not supported: TILT={data}";
                    }
                }

                string[] iesDataTokens = Regex.Split(iesReader.ReadToEnd().Trim(), @"[\s,]+");
                var iesDataTokenEnumerator = iesDataTokens.GetEnumerator();
                string iesDataToken;


                if (iesDataTokens.Length == 1 && string.IsNullOrWhiteSpace(iesDataTokens[0]))
                {
                    return "Premature end of file (missing IES data).";
                }

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing lamp count value).";
                }

                int lampCount;
                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!int.TryParse(iesDataToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out lampCount))
                {
                    return $"Invalid lamp count value: {iesDataToken}";
                }
                if (lampCount < 1) lampCount = 1;

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing lumens per lamp value).";
                }

                float lumensPerLamp;
                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out lumensPerLamp))
                {
                    return $"Invalid lumens per lamp value: {iesDataToken}";
                }
                m_TotalLumens = (lumensPerLamp < 0f) ? -1f : lampCount * lumensPerLamp;

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing candela multiplier value).";
                }

                float candelaMultiplier;
                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out candelaMultiplier))
                {
                    return $"Invalid candela multiplier value: {iesDataToken}";
                }
                if (candelaMultiplier < 0f) candelaMultiplier = 0f;

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing vertical angle count value).";
                }

                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!int.TryParse(iesDataToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out m_VerticalAngleCount))
                {
                    return $"Invalid vertical angle count value: {iesDataToken}";
                }
                if (m_VerticalAngleCount < 1)
                {
                    return $"Invalid number of vertical angles: {m_VerticalAngleCount}";
                }

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing horizontal angle count value).";
                }

                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!int.TryParse(iesDataToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out m_HorizontalAngleCount))
                {
                    return $"Invalid horizontal angle count value: {iesDataToken}";
                }
                if (m_HorizontalAngleCount < 1)
                {
                    return $"Invalid number of horizontal angles: {m_HorizontalAngleCount}";
                }

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing photometric type value).";
                }

                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!int.TryParse(iesDataToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out m_PhotometricType))
                {
                    return $"Invalid photometric type value: {iesDataToken}";
                }
                if (m_PhotometricType < 1 || m_PhotometricType > 3)
                {
                    return $"Invalid photometric type: {m_PhotometricType}";
                }

                // JHLEE ++++++++++
                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing luminous dimension unit type value).";
                }

                iesDataToken = iesDataTokenEnumerator.Current.ToString();

                float sizeUnitMultiplier = 0f;
                int sizeUnitType = 0;

                if (!int.TryParse(iesDataToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out sizeUnitType))
                {
                    return $"Invalid luminous dimension unit type value: {iesDataToken}";
                }

                if (sizeUnitType != 1 && sizeUnitType != 2)
                {
                    return $"Invalid luminous dimension unit type: {sizeUnitType}";
                }

                switch (sizeUnitType)
                {
                    case 1: // Feet
                        sizeUnitMultiplier = 0.3048f;
                        break;
                    case 2: // Meters
                        sizeUnitMultiplier = 1f;
                        break;
                }

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing luminous dimension width value).";
                }

                iesDataToken = iesDataTokenEnumerator.Current.ToString();

                if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out m_DimensionWidth))
                {
                    return $"Invalid luminous dimension width value: {iesDataToken}";
                }

                m_DimensionWidth *= sizeUnitMultiplier;

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing luminous dimension length value).";
                }

                iesDataToken = iesDataTokenEnumerator.Current.ToString();

                if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out m_DimensionLength))
                {
                    return $"Invalid luminous dimension length value: {iesDataToken}";
                }

                m_DimensionLength *= sizeUnitMultiplier;                

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing luminous dimension height value).";
                }                

                iesDataToken = iesDataTokenEnumerator.Current.ToString();

                if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out m_DimensionHeight))
                {
                    return $"Invalid luminous dimension height value: {iesDataToken}";
                }

                m_DimensionHeight *= sizeUnitMultiplier;                    
                // ++++++++++
                // JHLEE ----------
                // // Skip luminous dimension unit type.
                // if (!iesDataTokenEnumerator.MoveNext())
                // {
                //     return "Premature end of file (missing luminous dimension unit type value).";
                // }

                // // Skip luminous dimension width.
                // if (!iesDataTokenEnumerator.MoveNext())
                // {
                //     return "Premature end of file (missing luminous dimension width value).";
                // }

                // // Skip luminous dimension length.
                // if (!iesDataTokenEnumerator.MoveNext())
                // {
                //     return "Premature end of file (missing luminous dimension length value).";
                // }

                // // Skip luminous dimension height.
                // if (!iesDataTokenEnumerator.MoveNext())
                // {
                //     return "Premature end of file (missing luminous dimension height value).";
                // }
                // ----------

                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing ballast factor value).";
                }

                float ballastFactor;
                iesDataToken = iesDataTokenEnumerator.Current.ToString();
                if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out ballastFactor))
                {
                    return $"Invalid ballast factor value: {iesDataToken}";
                }
                if (ballastFactor < 0f) ballastFactor = 0f;

                // Skip future use.
                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing future use value).";
                }

                // Skip input watts.
                if (!iesDataTokenEnumerator.MoveNext())
                {
                    return "Premature end of file (missing input watts value).";
                }

                m_VerticalAngles = new float[m_VerticalAngleCount];
                float previousVerticalAngle = float.MinValue;

                m_MinDeltaVerticalAngle = 180f;

                for (int v = 0; v < m_VerticalAngleCount; ++v)
                {
                    if (!iesDataTokenEnumerator.MoveNext())
                    {
                        return "Premature end of file (missing vertical angle values).";
                    }

                    float angle;
                    iesDataToken = iesDataTokenEnumerator.Current.ToString();
                    if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out angle))
                    {
                        return $"Invalid vertical angle value: {iesDataToken}";
                    }

                    if (angle <= previousVerticalAngle)
                    {
                        return $"Vertical angles are not in ascending order near: {angle}";
                    }

                    float deltaVerticalAngle = angle - previousVerticalAngle;
                    if (deltaVerticalAngle < m_MinDeltaVerticalAngle)
                    {
                        m_MinDeltaVerticalAngle = deltaVerticalAngle;
                    }

                    m_VerticalAngles[v] = previousVerticalAngle = angle;
                }

                m_HorizontalAngles = new float[m_HorizontalAngleCount];
                float previousHorizontalAngle = float.MinValue;

                m_MinDeltaHorizontalAngle = 360f;

                for (int h = 0; h < m_HorizontalAngleCount; ++h)
                {
                    if (!iesDataTokenEnumerator.MoveNext())
                    {
                        return "Premature end of file (missing horizontal angle values).";
                    }

                    float angle;
                    iesDataToken = iesDataTokenEnumerator.Current.ToString();
                    if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out angle))
                    {
                        return $"Invalid horizontal angle value: {iesDataToken}";
                    }

                    if (angle <= previousHorizontalAngle)
                    {
                        return $"Horizontal angles are not in ascending order near: {angle}";
                    }

                    float deltaHorizontalAngle = angle - previousHorizontalAngle;
                    if (deltaHorizontalAngle < m_MinDeltaHorizontalAngle)
                    {
                        m_MinDeltaHorizontalAngle = deltaHorizontalAngle;
                    }

                    m_HorizontalAngles[h] = previousHorizontalAngle = angle;
                }

                m_FirstHorizontalAngle = m_HorizontalAngles[0];
                m_LastHorizontalAngle = m_HorizontalAngles[m_HorizontalAngleCount - 1];

                m_CandelaValues = new float[m_HorizontalAngleCount * m_VerticalAngleCount];
                m_MaxCandelas = 0f;

                for (int h = 0; h < m_HorizontalAngleCount; ++h)
                {
                    for (int v = 0; v < m_VerticalAngleCount; ++v)
                    {
                        if (!iesDataTokenEnumerator.MoveNext())
                        {
                            return "Premature end of file (missing candela values).";
                        }

                        float value;
                        iesDataToken = iesDataTokenEnumerator.Current.ToString();
                        if (!float.TryParse(iesDataToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
                        {
                            return $"Invalid candela value: {iesDataToken}";
                        }
                        value *= candelaMultiplier * ballastFactor;

                        m_CandelaValues[h * m_VerticalAngleCount + v] = value;

                        if (value > m_MaxCandelas)
                        {
                            m_MaxCandelas = value;
                        }
                    }
                }
            }

            return null;
        }

        internal string GetKeywordValue(string keyword)
        {
            return m_KeywordDictionary.ContainsKey(keyword) ? m_KeywordDictionary[keyword] : string.Empty;
        }

        internal int GetMinVerticalSampleCount()
        {
            if (m_PhotometricType == 2) // type B
            {
                // Factor in the 90 degree rotation that will be done when building the cylindrical texture.
                return 1 + (int)Mathf.Ceil(360 / m_MinDeltaHorizontalAngle); // 360 is 2 * 180 degrees
            }
            else // type A or C
            {
                return 1 + (int)Mathf.Ceil(360 / m_MinDeltaVerticalAngle); // 360 is 2 * 180 degrees
            }
        }

        internal int GetMinHorizontalSampleCount()
        {
            switch (m_PhotometricType)
            {
                case 3: // type A
                    return 1 + (int)Mathf.Ceil(720 / m_MinDeltaHorizontalAngle); // 720 is 2 * 360 degrees
                case 2: // type B
                    // Factor in the 90 degree rotation that will be done when building the cylindrical texture.
                    return 1 + (int)Mathf.Ceil(720 / m_MinDeltaVerticalAngle); // 720 is 2 * 360 degrees
                default: // type C
                    // Factor in the 90 degree rotation that will be done when building the cylindrical texture.
                    return 1 + (int)Mathf.Ceil(720 / Mathf.Min(m_MinDeltaHorizontalAngle, m_MinDeltaVerticalAngle)); // 720 is 2 * 360 degrees
            }
        }

        internal float ComputeVerticalAnglePosition(float angle)
        {
            return ComputeAnglePosition(angle, m_VerticalAngles);
        }

        internal float ComputeTypeAorBHorizontalAnglePosition(float angle) // angle in range [-180..+180] degrees
        {
            return ComputeAnglePosition(((m_FirstHorizontalAngle == 0f) ? Mathf.Abs(angle) : angle), m_HorizontalAngles);
        }

        internal float ComputeTypeCHorizontalAnglePosition(float angle) // angle in range [0..360] degrees
        {
            switch (m_LastHorizontalAngle)
            {
                case 0f: // the luminaire is assumed to be laterally symmetric in all planes
                    angle = 0f;
                    break;
                case 90f: // the luminaire is assumed to be symmetric in each quadrant
                    angle = 90f - Mathf.Abs(Mathf.Abs(angle - 180f) - 90f);
                    break;
                case 180f: // the luminaire is assumed to be symmetric about the 0 to 180 degree plane
                    angle = 180f - Mathf.Abs(angle - 180f);
                    break;
                default: // the luminaire is assumed to exhibit no lateral symmetry
                    break;
            }

            return ComputeAnglePosition(angle, m_HorizontalAngles);
        }

        internal float ComputeAnglePosition(float value, float[] angles)
        {
            int start = 0;
            int end = angles.Length - 1;

            if (value < angles[start])
            {
                return start;
            }

            if (value > angles[end])
            {
                return end;
            }

            while (start < end)
            {
                int index = (start + end + 1) / 2;

                float angle = angles[index];

                if (value >= angle)
                {
                    start = index;
                }
                else
                {
                    end = index - 1;
                }
            }

            float leftValue = angles[start];
            float fraction = 0f;

            if (start + 1 < angles.Length)
            {
                float rightValue = angles[start + 1];
                float deltaValue = rightValue - leftValue;

                if (deltaValue > 0.0001f)
                {
                    fraction = (value - leftValue) / deltaValue;
                }
            }

            return start + fraction;
        }

        internal float InterpolateBilinear(float x, float y)
        {
            int ix = (int)Mathf.Floor(x);
            int iy = (int)Mathf.Floor(y);

            float fractionX = x - ix;
            float fractionY = y - iy;

            float p00 = InterpolatePoint(ix + 0, iy + 0);
            float p10 = InterpolatePoint(ix + 1, iy + 0);
            float p01 = InterpolatePoint(ix + 0, iy + 1);
            float p11 = InterpolatePoint(ix + 1, iy + 1);

            float p0 = Mathf.Lerp(p00, p01, fractionY);
            float p1 = Mathf.Lerp(p10, p11, fractionY);

            return Mathf.Lerp(p0, p1, fractionX);
        }

        internal float InterpolatePoint(int x, int y)
        {
            x %= m_HorizontalAngles.Length;
            y %= m_VerticalAngles.Length;

            return m_CandelaValues[y + x * m_VerticalAngles.Length];
        }
    }

#if IES_ENGINE_DEFINITION
    [System.Serializable]
    public class IESEngine
    {
        const float k_HalfPi = 0.5f * Mathf.PI;
        const float k_TwoPi = 2.0f * Mathf.PI;

        internal IESReader m_iesReader = new IESReader();

        internal string FileFormatVersion { get => m_iesReader.FileFormatVersion; }

        // DEL
        //internal TextureImporterType m_TextureGenerationType = TextureImporterType.Cookie;

        // DEL
        /// <summary>
        /// setter for the Texture generation Type
        /// </summary>
        // public TextureImporterType TextureGenerationType
        // {
        //     set { m_TextureGenerationType = value; }
        // }

        /// <summary>
        /// Method to read the IES File
        /// </summary>
        /// <param name="iesFilePath">Path to the IES file in the Disk.</param>
        /// <returns>An error message or warning otherwise null if no error</returns>
        public string ReadFile(string iesFilePath)
        {
            if (!File.Exists(iesFilePath))
            {
                return "IES file does not exist.";
            }

            string errorMessage;

            try
            {
                errorMessage = m_iesReader.ReadFile(iesFilePath);
            }
            catch (IOException ioEx)
            {
                return ioEx.Message;
            }

            return errorMessage;
        }

        /// <summary>
        /// Check a keyword
        /// </summary>
        /// <param name="keyword">A keyword to check if exist.</param>
        /// <returns>A Keyword if exist inside the internal Dictionary</returns>
        public string GetKeywordValue(string keyword)
        {
            return m_iesReader.GetKeywordValue(keyword);
        }

        /// <summary>
        /// Getter (as a string) for the Photometric Type
        /// </summary>
        /// <returns>The current Photometric Type</returns>
        public string GetPhotometricType()
        {
            switch (m_iesReader.PhotometricType)
            {
                case 3: // type A
                    return "Type A";
                case 2: // type B
                    return "Type B";
                default: // type C
                    return "Type C";
            }
        }

        // JHLEE ++++++++++
        public float GetDimensionWidth()
        {
            return m_iesReader.DimensionWidth;
        }

        public float GetDimensionLength()
        {
            return m_iesReader.DimensionLength;
        }

        public float GetDimensionHeight()
        {
            return m_iesReader.DimensionHeight;
        }        
        // ++++++++++

        /// <summary>
        /// Get the CUrrent Max intensity
        /// </summary>
        /// <returns>A pair of the intensity follow by the used unit (candelas or lumens)</returns>
        public (float, string) GetMaximumIntensity()
        {
            // JHLEE ++++++++++
            return (m_iesReader.MaxCandelas, "Candelas");
            // ++++++++++
            // JHLEE ----------
            // if (m_iesReader.TotalLumens == -1f) // absolute photometry
            // {
            //     return (m_iesReader.MaxCandelas, "Candelas");
            // }
            // else
            // {
            //     return (m_iesReader.TotalLumens, "Lumens");
            // }
            // ----------
        }

        // DEL
        /// <summary>
        /// Generated a Cube texture based on the internal PhotometricType
        /// </summary>
        /// <param name="compression">Compression parameter requestted.</param>
        /// <param name="textureSize">The resquested size.</param>
        /// <returns>A Cubemap representing this IES</returns>
        // public (string, Texture) GenerateCubeCookie(TextureImporterCompression compression, int textureSize)
        // {
        //     int width = 2 * textureSize;
        //     int height = 2 * textureSize;

        //     NativeArray<Color32> colorBuffer;

        //     switch (m_iesReader.PhotometricType)
        //     {
        //         case 3: // type A
        //             colorBuffer = BuildTypeACylindricalTexture(width, height);
        //             break;
        //         case 2: // type B
        //             colorBuffer = BuildTypeBCylindricalTexture(width, height);
        //             break;
        //         default: // type C
        //             colorBuffer = BuildTypeCCylindricalTexture(width, height);
        //             break;
        //     }

        //     return GenerateTexture(m_TextureGenerationType, TextureImporterShape.TextureCube, compression, width, height, colorBuffer);
        // }

        public Color ToColor(Color32 color)
        {            
            return new Color((float)color.r / 255f, (float)color.g / 255f, (float)color.b / 255f, (float)color.a / 255f);
        }

        public Cubemap GenerateCubeCookie(int textureSize)
        {
            int width = 2 * textureSize;
            int height = 2 * textureSize;

            NativeArray<Color32> colorBuffer;

            switch (m_iesReader.PhotometricType)
            {
                case 3: // type A
                    colorBuffer = BuildTypeACylindricalTexture(width, height);
                    break;
                case 2: // type B
                    colorBuffer = BuildTypeBCylindricalTexture(width, height);
                    break;
                default: // type C
                    colorBuffer = BuildTypeCCylindricalTexture(width, height);
                    break;
            }

            var cubemap = new Cubemap(textureSize, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, UnityEngine.Experimental.Rendering.TextureCreationFlags.None, 1);
            float[] worldFloats = new float[3];
            float[] bilinear = new float[4];
            
            for (int face = 0; face < 6; ++face)
            {                
                var faceBuffer = new NativeArray<Color32>(textureSize * textureSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                for (int v = 0; v < textureSize; ++v)
                {                    
                    for (int h = 0; h < textureSize; ++h)
                    {    
                        Vector3 uvDir = new Vector3((float)h / textureSize, (float)v / textureSize, 0f);
                        uvDir = uvDir * 2f - new Vector3(1f, 1f, 0f);
                        uvDir.z = 1f;
                        uvDir = new Vector3(uvDir.x * kCubeSigns[face].x, uvDir.y * kCubeSigns[face].y, uvDir.z * kCubeSigns[face].z);
                        worldFloats[kCubeRemap[face * 3 + 0]] = uvDir.x;
                        worldFloats[kCubeRemap[face * 3 + 1]] = uvDir.y;
                        worldFloats[kCubeRemap[face * 3 + 2]] = uvDir.z;

                        Vector3 worldDir = new Vector3(worldFloats[0], worldFloats[1], worldFloats[2]);
                        worldDir = worldDir.normalized;

                        Vector2 pixelUV = new Vector2();
                        pixelUV.x = 0.75f - Mathf.Atan2(worldDir.z, worldDir.x) / (Mathf.PI * 2f);

                        if (pixelUV.x > 1f)
                        {
                            pixelUV.x -= 1f;
                        }

                        pixelUV.y = 1f - Mathf.Acos(worldDir.y) / Mathf.PI;
                        pixelUV = new Vector2(pixelUV.x * width, pixelUV.y * height);

                        int x0 = Mathf.FloorToInt(pixelUV.x);
                        int y0 = Mathf.FloorToInt(pixelUV.y);
                        int x1, y1;
                        float fracX = pixelUV.x - (float)x0;
                        float fracY = pixelUV.y - (float)y0;

                        x0 = Mathf.Clamp(x0, 0, width - 1);
                        x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
                        y0 = Mathf.Clamp(y0, 0, height - 1);
                        y1 = Mathf.Clamp(y0 + 1, 0, height - 1);

                        float oneFracX = 1f - fracX;
                        float oneFracY = 1f - fracY;
                        bilinear[0] = oneFracX * oneFracY;
                        bilinear[1] = fracX * oneFracY;
                        bilinear[2] = oneFracX * fracY;
                        bilinear[3] = fracX * fracY;

                        Color color;
                        color = bilinear[0] * ToColor(colorBuffer[y0 * width + x0]);
                        color += bilinear[1] * ToColor(colorBuffer[y0 * width + x1]);
                        color += bilinear[2] * ToColor(colorBuffer[y1 * width + x0]);
                        color += bilinear[3] * ToColor(colorBuffer[y1 * width + x1]);

                        faceBuffer[v * textureSize + h] = color;
                    }
                }

                cubemap.SetPixelData(faceBuffer, 0, (CubemapFace)face);
            }
            
            cubemap.Apply();
            return cubemap;
        }           

        // DEL
        // Gnomonic projection reference:
        // http://speleotrove.com/pangazer/gnomonic_projection.html
        /// <summary>
        /// Generating a 2D Texture of this cookie, using a Gnomonic projection of the bottom of the IES
        /// </summary>
        /// <param name="compression">Compression parameter requestted.</param>
        /// <param name="coneAngle">Cone angle used to performe the Gnomonic projection.</param>
        /// <param name="textureSize">The resquested size.</param>
        /// <param name="applyLightAttenuation">Bool to enable or not the Light Attenuation based on the squared distance.</param>
        /// <returns>A Generated 2D texture doing the projection of the IES using the Gnomonic projection of the bottom half hemisphere with the given 'cone angle'</returns>
        // public (string, Texture) Generate2DCookie(TextureImporterCompression compression, float coneAngle, int textureSize, bool applyLightAttenuation)
        // {
        //     NativeArray<Color32> colorBuffer;

        //     switch (m_iesReader.PhotometricType)
        //     {
        //         case 3: // type A
        //             colorBuffer = BuildTypeAGnomonicTexture(coneAngle, textureSize, applyLightAttenuation);
        //             break;
        //         case 2: // type B
        //             colorBuffer = BuildTypeBGnomonicTexture(coneAngle, textureSize, applyLightAttenuation);
        //             break;
        //         default: // type C
        //             colorBuffer = BuildTypeCGnomonicTexture(coneAngle, textureSize, applyLightAttenuation);
        //             break;
        //     }

        //     return GenerateTexture(m_TextureGenerationType, TextureImporterShape.Texture2D, compression, textureSize, textureSize, colorBuffer);
        // }

        public Texture2D Generate2DCookie(float coneAngle, int textureSize, bool applyLightAttenuation)
        {
            NativeArray<Color32> colorBuffer;

            switch (m_iesReader.PhotometricType)
            {
                case 3: // type A
                    colorBuffer = BuildTypeAGnomonicTexture(coneAngle, textureSize, applyLightAttenuation);
                    break;
                case 2: // type B
                    colorBuffer = BuildTypeBGnomonicTexture(coneAngle, textureSize, applyLightAttenuation);
                    break;
                default: // type C
                    colorBuffer = BuildTypeCGnomonicTexture(coneAngle, textureSize, applyLightAttenuation);
                    break;
            }

            var tex = new Texture2D(textureSize, textureSize, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, 1, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            tex.SetPixelData<Color32>(colorBuffer, 0);
            tex.Apply();
            return tex;
        }        

        // DEL
        // private (string, Texture) GenerateCylindricalTexture(TextureImporterCompression compression, int textureSize)
        // {
        //     int width = 2 * textureSize;
        //     int height = textureSize;

        //     NativeArray<Color32> colorBuffer;

        //     switch (m_iesReader.PhotometricType)
        //     {
        //         case 3: // type A
        //             colorBuffer = BuildTypeACylindricalTexture(width, height);
        //             break;
        //         case 2: // type B
        //             colorBuffer = BuildTypeBCylindricalTexture(width, height);
        //             break;
        //         default: // type C
        //             colorBuffer = BuildTypeCCylindricalTexture(width, height);
        //             break;
        //     }

        //     return GenerateTexture(TextureImporterType.Default, TextureImporterShape.Texture2D, compression, width, height, colorBuffer);
        // }

        // DEL
        // (string, Texture) GenerateTexture(TextureImporterType type, TextureImporterShape shape, TextureImporterCompression compression, int width, int height, NativeArray<Color32> colorBuffer)
        // {
        //     // Default values set by the TextureGenerationSettings constructor can be found in this file on GitHub:
        //     // https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/AssetPipeline/TextureGenerator.bindings.cs

        //     var settings = new TextureGenerationSettings(type);

        //     SourceTextureInformation textureInfo = settings.sourceTextureInformation;
        //     textureInfo.containsAlpha = true;
        //     textureInfo.height = height;
        //     textureInfo.width = width;

        //     TextureImporterSettings textureImporterSettings = settings.textureImporterSettings;
        //     textureImporterSettings.alphaSource = TextureImporterAlphaSource.FromInput;
        //     textureImporterSettings.aniso = 0;
        //     textureImporterSettings.borderMipmap = (textureImporterSettings.textureType == TextureImporterType.Cookie);
        //     textureImporterSettings.filterMode = FilterMode.Bilinear;
        //     textureImporterSettings.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
        //     textureImporterSettings.mipmapEnabled = false;
        //     textureImporterSettings.npotScale = TextureImporterNPOTScale.None;
        //     textureImporterSettings.readable = true;
        //     textureImporterSettings.sRGBTexture = false;
        //     textureImporterSettings.textureShape = shape;
        //     textureImporterSettings.wrapMode = textureImporterSettings.wrapModeU = textureImporterSettings.wrapModeV = textureImporterSettings.wrapModeW = TextureWrapMode.Clamp;

        //     TextureImporterPlatformSettings platformSettings = settings.platformSettings;
        //     platformSettings.maxTextureSize = 2048;
        //     platformSettings.resizeAlgorithm = TextureResizeAlgorithm.Bilinear;
        //     platformSettings.textureCompression = compression;

        //     TextureGenerationOutput output = TextureGenerator.GenerateTexture(settings, colorBuffer);

        //     if (output.importWarnings.Length > 0)
        //     {
        //         Debug.LogWarning("Cannot properly generate IES texture:\n" + string.Join("\n", output.importWarnings));
        //     }

        //     return (output.importInspectorWarnings, output.texture);
        // }

        NativeArray<Color32> BuildTypeACylindricalTexture(int width, int height)
        {
            float stepU = 360f / (width - 1);
            float stepV = 180f / (height - 1);

            var textureBuffer = new NativeArray<Color32>(width * height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (int y = 0; y < height; y++)
            {
                var slice = new NativeSlice<Color32>(textureBuffer, y * width, width);

                float latitude = y * stepV - 90f; // in range [-90..+90] degrees

                float verticalAnglePosition = m_iesReader.ComputeVerticalAnglePosition(latitude);

                for (int x = 0; x < width; x++)
                {
                    float longitude = x * stepU - 180f; // in range [-180..+180] degrees

                    float horizontalAnglePosition = m_iesReader.ComputeTypeAorBHorizontalAnglePosition(longitude);

                    byte value = (byte)((m_iesReader.InterpolateBilinear(horizontalAnglePosition, verticalAnglePosition) / m_iesReader.MaxCandelas) * 255);
                    slice[x] = new Color32(value, value, value, value);
                }
            }

            return textureBuffer;
        }

        NativeArray<Color32> BuildTypeBCylindricalTexture(int width, int height)
        {
            float stepU = k_TwoPi / (width - 1);
            float stepV = Mathf.PI / (height - 1);

            var textureBuffer = new NativeArray<Color32>(width * height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (int y = 0; y < height; y++)
            {
                var slice = new NativeSlice<Color32>(textureBuffer, y * width, width);

                float v = y * stepV - k_HalfPi; // in range [-90..+90] degrees

                float sinV = Mathf.Sin(v);
                float cosV = Mathf.Cos(v);

                for (int x = 0; x < width; x++)
                {
                    float u = Mathf.PI - x * stepU; // in range [+180..-180] degrees

                    float sinU = Mathf.Sin(u);
                    float cosU = Mathf.Cos(u);

                    // Since a type B luminaire is turned on its side, rotate it to make its polar axis horizontal.
                    float longitude = Mathf.Atan2(sinV, cosU * cosV) * Mathf.Rad2Deg; // in range [-180..+180] degrees
                    float latitude = Mathf.Asin(-sinU * cosV) * Mathf.Rad2Deg;        // in range [-90..+90] degrees

                    float horizontalAnglePosition = m_iesReader.ComputeTypeAorBHorizontalAnglePosition(longitude);
                    float verticalAnglePosition = m_iesReader.ComputeVerticalAnglePosition(latitude);

                    byte value = (byte)((m_iesReader.InterpolateBilinear(horizontalAnglePosition, verticalAnglePosition) / m_iesReader.MaxCandelas) * 255);
                    slice[x] = new Color32(value, value, value, value);
                }
            }

            return textureBuffer;
        }

        NativeArray<Color32> BuildTypeCCylindricalTexture(int width, int height)
        {
            float stepU = k_TwoPi / (width - 1);
            float stepV = Mathf.PI / (height - 1);

            var textureBuffer = new NativeArray<Color32>(width * height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            for (int y = 0; y < height; y++)
            {
                var slice = new NativeSlice<Color32>(textureBuffer, y * width, width);

                float v = y * stepV - k_HalfPi; // in range [-90..+90] degrees

                float sinV = Mathf.Sin(v);
                float cosV = Mathf.Cos(v);

                for (int x = 0; x < width; x++)
                {
                    float u = Mathf.PI - x * stepU; // in range [+180..-180] degrees

                    float sinU = Mathf.Sin(u);
                    float cosU = Mathf.Cos(u);

                    // Since a type C luminaire is generally aimed at nadir, orient it toward +Z at the center of the cylindrical texture.
                    float longitude = ((Mathf.Atan2(sinU * cosV, sinV) + k_TwoPi) % k_TwoPi) * Mathf.Rad2Deg; // in range [0..360] degrees
                    float latitude = (Mathf.Asin(-cosU * cosV) + k_HalfPi) * Mathf.Rad2Deg;                  // in range [0..180] degrees

                    float horizontalAnglePosition = m_iesReader.ComputeTypeCHorizontalAnglePosition(longitude);
                    float verticalAnglePosition = m_iesReader.ComputeVerticalAnglePosition(latitude);

                    byte value = (byte)((m_iesReader.InterpolateBilinear(horizontalAnglePosition, verticalAnglePosition) / m_iesReader.MaxCandelas) * 255);
                    slice[x] = new Color32(value, value, value, value);
                }
            }

            return textureBuffer;
        }

        NativeArray<Color32> BuildTypeAGnomonicTexture(float coneAngle, int size, bool applyLightAttenuation)
        {
            float limitUV = Mathf.Tan(0.5f * coneAngle * Mathf.Deg2Rad);
            float stepUV = (2 * limitUV) / (size - 3);

            var textureBuffer = new NativeArray<Color32>(size * size, Allocator.Temp, NativeArrayOptions.ClearMemory);

            // Leave a one-pixel black border around the texture to avoid cookie spilling.
            for (int y = 1; y < size - 1; y++)
            {
                var slice = new NativeSlice<Color32>(textureBuffer, y * size, size);

                float v = (y - 1) * stepUV - limitUV;

                for (int x = 1; x < size - 1; x++)
                {
                    float u = (x - 1) * stepUV - limitUV;

                    float rayLengthSquared = u * u + v * v + 1;

                    float longitude = Mathf.Atan(u) * Mathf.Rad2Deg;                               // in range [-90..+90] degrees
                    float latitude = Mathf.Asin(v / Mathf.Sqrt(rayLengthSquared)) * Mathf.Rad2Deg; // in range [-90..+90] degrees

                    float horizontalAnglePosition = m_iesReader.ComputeTypeCHorizontalAnglePosition(longitude);
                    float verticalAnglePosition = m_iesReader.ComputeVerticalAnglePosition(latitude);

                    // Factor in the light attenuation further from the texture center.
                    float lightAttenuation = applyLightAttenuation ? rayLengthSquared : 1f;

                    byte value = (byte)((m_iesReader.InterpolateBilinear(horizontalAnglePosition, verticalAnglePosition) / (m_iesReader.MaxCandelas * lightAttenuation)) * 255);
                    slice[x] = new Color32(value, value, value, value);
                }
            }

            return textureBuffer;
        }

        NativeArray<Color32> BuildTypeBGnomonicTexture(float coneAngle, int size, bool applyLightAttenuation)
        {
            float limitUV = Mathf.Tan(0.5f * coneAngle * Mathf.Deg2Rad);
            float stepUV = (2 * limitUV) / (size - 3);

            var textureBuffer = new NativeArray<Color32>(size * size, Allocator.Temp, NativeArrayOptions.ClearMemory);

            // Leave a one-pixel black border around the texture to avoid cookie spilling.
            for (int y = 1; y < size - 1; y++)
            {
                var slice = new NativeSlice<Color32>(textureBuffer, y * size, size);

                float v = (y - 1) * stepUV - limitUV;

                for (int x = 1; x < size - 1; x++)
                {
                    float u = (x - 1) * stepUV - limitUV;

                    float rayLengthSquared = u * u + v * v + 1;

                    // Since a type B luminaire is turned on its side, U and V are flipped.
                    float longitude = Mathf.Atan(v) * Mathf.Rad2Deg;                               // in range [-90..+90] degrees
                    float latitude = Mathf.Asin(u / Mathf.Sqrt(rayLengthSquared)) * Mathf.Rad2Deg; // in range [-90..+90] degrees

                    float horizontalAnglePosition = m_iesReader.ComputeTypeCHorizontalAnglePosition(longitude);
                    float verticalAnglePosition = m_iesReader.ComputeVerticalAnglePosition(latitude);

                    // Factor in the light attenuation further from the texture center.
                    float lightAttenuation = applyLightAttenuation ? rayLengthSquared : 1f;

                    byte value = (byte)((m_iesReader.InterpolateBilinear(horizontalAnglePosition, verticalAnglePosition) / (m_iesReader.MaxCandelas * lightAttenuation)) * 255);
                    slice[x] = new Color32(value, value, value, value);
                }
            }

            return textureBuffer;
        }

        NativeArray<Color32> BuildTypeCGnomonicTexture(float coneAngle, int size, bool applyLightAttenuation)
        {
            float limitUV = Mathf.Tan(0.5f * coneAngle * Mathf.Deg2Rad);
            float stepUV = (2 * limitUV) / (size - 3);

            var textureBuffer = new NativeArray<Color32>(size * size, Allocator.Temp, NativeArrayOptions.ClearMemory);

            // Leave a one-pixel black border around the texture to avoid cookie spilling.
            for (int y = 1; y < size - 1; y++)
            {
                var slice = new NativeSlice<Color32>(textureBuffer, y * size, size);

                float v = (y - 1) * stepUV - limitUV;

                for (int x = 1; x < size - 1; x++)
                {
                    float u = (x - 1) * stepUV - limitUV;

                    float uvLength = Mathf.Sqrt(u * u + v * v);

                    float longitude = ((Mathf.Atan2(v, u) - k_HalfPi + k_TwoPi) % k_TwoPi) * Mathf.Rad2Deg; // in range [0..360] degrees
                    float latitude = Mathf.Atan(uvLength) * Mathf.Rad2Deg;                                  // in range [0..90] degrees

                    float horizontalAnglePosition = m_iesReader.ComputeTypeCHorizontalAnglePosition(longitude);
                    float verticalAnglePosition = m_iesReader.ComputeVerticalAnglePosition(latitude);

                    // Factor in the light attenuation further from the texture center.
                    float lightAttenuation = applyLightAttenuation ? (uvLength * uvLength + 1) : 1f;

                    byte value = (byte)((m_iesReader.InterpolateBilinear(horizontalAnglePosition, verticalAnglePosition) / (m_iesReader.MaxCandelas * lightAttenuation)) * 255);
                    slice[x] = new Color32(value, value, value, value);
                }
            }

            return textureBuffer;
        }

        static readonly Vector3[] kCubeSigns = new Vector3[] 
        {    
            new Vector3(-1f, -1f, 1f),
            new Vector3(1f, -1f, -1f),
            new Vector3(1f, 1f, 1f),
            new Vector3(1f, -1f, -1f),
            new Vector3(1f, -1f, 1f),
            new Vector3(-1f, -1f, -1f),
        };        

        static readonly int[] kCubeRemap = new int[]
        {
            2, 1, 0,
            2, 1, 0,
            0, 2, 1,
            0, 2, 1,
            0, 1, 2,
            0, 1, 2,
        };
    }
#endif
}
