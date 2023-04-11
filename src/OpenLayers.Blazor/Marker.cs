﻿using Microsoft.AspNetCore.Components;

namespace OpenLayers.Blazor;

public class Marker : Shape<Internal.Marker>
{
    public Marker() : base(new Internal.Marker())
    {
    }

    [Parameter]
    public MarkerType Type
    {
        get => Enum.Parse<MarkerType>(InternalFeature.Kind);
        set => InternalFeature.Kind = value.ToString();
    }

    [Parameter]
    public Coordinate Coordinate
    {
        get => InternalFeature.Coordinate;
        set => InternalFeature.Coordinate = value;
    }
}