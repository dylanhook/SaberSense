// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

internal static class SvgPathParser
{
    public static List<List<Vector2>> Parse(string d)
    {
        var contours = new List<List<Vector2>>();
        var current = new List<Vector2>();

        int i = 0;
        while (i < d.Length)
        {
            char c = d[i];

            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
                continue;
            }

            switch (c)
            {
                case 'M':
                {
                    i++;
                    if (current.Count is > 0)
                    contours.Add(current);
                    current = [];
                    float x = ReadFloat(d, ref i);
                    float y = ReadFloat(d, ref i);
                    current.Add(new Vector2(x, y));
                    break;
                }

                case 'L':
                {
                    i++;
                    float x = ReadFloat(d, ref i);
                    float y = ReadFloat(d, ref i);
                    current.Add(new Vector2(x, y));
                    break;
                }

                case 'Z':
                case 'z':
                i++;
                break;

                default:
                i++;
                break;
            }
        }

        if (current.Count is > 0)
        contours.Add(current);

        return contours;
    }

    private static float ReadFloat(string s, ref int i)
    {
        while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ','))
        i++;

        int start = i;

        if (i < s.Length && (s[i] == '-' || s[i] == '+'))
        i++;

        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
        i++;

        if (i == start) return 0f;
        return float.Parse(s[start..i], CultureInfo.InvariantCulture);
    }
}