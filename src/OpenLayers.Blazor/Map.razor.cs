﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace OpenLayers.Blazor;

/// <summary>
///     Component to show OpenLayers Maps
/// </summary>
public partial class Map : IAsyncDisposable
{
    private string _mapId;
    private IJSObjectReference? _module;
    private Feature? _popupContext;
    private string _popupId;

    /// <summary>
    ///     Default Constructor
    /// </summary>
    public Map()
    {
        _mapId = Guid.NewGuid().ToString();
        _popupId = Guid.NewGuid().ToString();

        EnableShapeSnap = true;
    }

    [Inject] private IJSRuntime? JSRuntime { get; set; }

    /// <summary>
    ///     Gets or set then center of the map
    /// </summary>
    [Parameter]
    public Coordinate Center { get; set; } = new(0, 0);

    /// <summary>
    ///     Event when center changes
    /// </summary>
    [Parameter]
    public EventCallback<Coordinate> CenterChanged { get; set; }

    /// <summary>
    ///     Zoom level of the map
    /// </summary>
    [Parameter]
    public double Zoom { get; set; } = 2;

    /// <summary>
    ///     Event on zoom changes
    /// </summary>
    [Parameter]
    public EventCallback<double> ZoomChanged { get; set; }

    [Parameter] public EventCallback<Extent> VisibleExtentChanged { get; set; }

    /// <summary>
    ///     Collection of attached markers
    /// </summary>
    public ObservableCollection<Marker> MarkersList { get; } = new();

    /// <summary>
    ///     Collection of attached shapes
    /// </summary>
    public ObservableCollection<Shape> ShapesList { get; } = new();

    /// <summary>
    ///     Event when a feature (shapes/markers) is called
    /// </summary>
    [Parameter]
    public EventCallback<Feature> OnFeatureClick { get; set; }

    /// <summary>
    ///     Event when a marker get clicked
    /// </summary>
    [Parameter]
    public EventCallback<Marker> OnMarkerClick { get; set; }

    /// <summary>
    ///     Event when a shape gets clicked
    /// </summary>
    [Parameter]
    public EventCallback<Internal.Shape> OnShapeClick { get; set; }

    /// <summary>
    ///     Event when a point in the map gets clicked. Event returns current coordinates
    /// </summary>
    [Parameter]
    public EventCallback<Coordinate> OnClick { get; set; }

    /// <summary>
    ///     Event when the pointer gets moved
    /// </summary>
    [Parameter]
    public EventCallback<Coordinate> OnPointerMove { get; set; }

    /// <summary>
    ///     Event when the rendering is complete
    /// </summary>
    [Parameter]
    public EventCallback OnRenderComplete { get; set; }

    /// <summary>
    ///     Content to show as a popup when a shape or marker gets clicked and <see cref="Shape.Popup" /> is set to true
    /// </summary>
    [Parameter]
    public RenderFragment<Feature?>? Popup { get; set; }

    /// <summary>
    ///     Definition of Layers to show in the map. Only items of <see cref="Layer" /> are considered.
    /// </summary>
    /// <example>
    ///     <Layers>
    ///         <Layer SourceType="SourceType.TileWMS"
    ///             Url="https://sedac.ciesin.columbia.edu/geoserver/ows?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&FORMAT=image%2Fpng&TRANSPARENT=true&LAYERS=gpw-v3%3Agpw-v3-population-density_2000&LANG=en"
    ///             Opacity=".3"
    ///             CrossOrigin="anonymous" />
    ///     </Layers>
    /// </example>
    [Parameter]
    public RenderFragment? Layers { get; set; }

    /// <summary>
    ///     Definition of Features to show on the map. Only items of the type <see cref="Marker" /> or <see cref="Shape" /> (
    ///     <see cref="Line" />, <see cref="Circle" />) are considered.
    /// </summary>
    /// <example>
    ///     <Features>
    ///         <Marker Type="MarkerType.MarkerPin" Coordinate="new Coordinate(1197650, 2604200)"></Marker>
    ///         <Line Points="new []{new Coordinate(1197650, 2604200), new Coordinate(1177650, 2624200)}" BorderColor="cyan"></Line>
    ///     </Features>
    /// </example>
    [Parameter]
    public RenderFragment? Features { get; set; }

    /// <summary>
    ///     Collection of all Layers
    /// </summary>
    public ObservableCollection<Layer> LayersList { get; } = new();

    /// <summary>
    ///     Defaults to use for the map rendering
    /// </summary>
    public Defaults Defaults { get; } = new();

    /// <summary>
    ///     Class of the map element
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    ///     Styles of the map element
    /// </summary>
    [Parameter]
    public string? Style { get; set; }

    /// <summary>
    ///     Coordinates projection to use of the layers and events. Default is set to "EPSG:4326" (https://epsg.io/4326).
    ///     Additionally to the default OpenLayers projections, the swiss projections EPSG:2056 (VT95) and EPSG:21781 (VT03)
    ///     are supported.
    /// </summary>
    [Parameter]
    public string CoordinatesProjection
    {
        get => Defaults.CoordinatesProjection;
        set => Defaults.CoordinatesProjection = value;
    }

    /// <summary>
    ///     Unit of the ScaleLine
    /// </summary>
    [Parameter]
    public ScaleLineUnit ScaleLineUnit
    {
        get => Defaults.ScaleLineUnit;
        set => Defaults.ScaleLineUnit = value;
    }

    /// <summary>
    ///     Sets or gets the visible extent of the map
    /// </summary>
    [Parameter]
    public Extent? VisibleExtent { get; set; }

    /// <summary>
    ///     A shape providing default parameters when drawing new shapes
    /// </summary>
    [Parameter]
    public ShapeType NewShapeType { get; set; }

    /// <summary>
    ///     Get or set if new shapes shall be drawn
    /// </summary>
    [Parameter]
    public bool EnableNewShapes { get; set; }

    /// <summary>
    ///     Get or sets if the position of points shall be snapped.
    /// </summary>
    [Parameter]
    public bool EnableShapeSnap { get; set; }

    /// <summary>
    ///     Get or sets if drawing new shapes is enabled.
    /// </summary>
    [Parameter]
    public bool EnableEditShapes { get; set; }

    private DotNetObjectReference<Map>? Instance { get; set; }

    [Parameter] public EventCallback<Shape> OnShapeAdded { get; set; }

    [Parameter] public EventCallback<Shape> OnShapeChanged { get; set; }

    [Parameter] public Func<Shape, StyleOptions> ShapeStyleCallback { get; set; } = DefaultShapeStyleCallback;

    /// <summary>
    ///     Disposing resources.
    /// </summary>
    /// <returns>ValueTask</returns>
    public async ValueTask DisposeAsync()
    {
        MarkersList.CollectionChanged -= MarkersOnCollectionChanged;
        ShapesList.CollectionChanged -= ShapesOnCollectionChanged;
        LayersList.CollectionChanged -= LayersOnCollectionChanged;

        if (_module != null)
        {
            await _module.InvokeVoidAsync("MapOLDispose", _mapId);
            await _module.DisposeAsync();
            _module = null;
        }
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (parameters.TryGetValue(nameof(Zoom), out double zoom) && zoom != Zoom)
            _ = SetZoom(zoom);

        if (parameters.TryGetValue(nameof(Center), out Coordinate center) && !center.Equals(Center))
            _ = SetCenter(center);

        if (parameters.TryGetValue(nameof(VisibleExtent), out Extent extent) && !extent.Equals(VisibleExtent))
            _ = SetVisibleExtent(extent);

        short drawingChanges = 0;
        if (parameters.TryGetValue(nameof(EnableNewShapes), out bool newShapes) && newShapes != EnableNewShapes)
            drawingChanges++;
        else
            newShapes = EnableNewShapes;

        if (parameters.TryGetValue(nameof(EnableEditShapes), out bool editShapes) && editShapes != EnableEditShapes)
            drawingChanges++;
        else
            editShapes = EnableEditShapes;

        if (parameters.TryGetValue(nameof(EnableShapeSnap), out bool shapeSnap) && shapeSnap != EnableShapeSnap)
            drawingChanges++;
        else
            shapeSnap = EnableShapeSnap;

        if (parameters.TryGetValue(nameof(NewShapeType), out ShapeType shapeType) && shapeType != NewShapeType)
            drawingChanges++;
        else
            shapeType = NewShapeType;

        if (drawingChanges > 0)
            _ = SetDrawingSettings(newShapes, editShapes, shapeSnap, shapeType);

        return base.SetParametersAsync(parameters);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _module ??= await JSRuntime!.InvokeAsync<IJSObjectReference>("import", $"./_content/{Assembly.GetExecutingAssembly().GetName().Name}/openlayers_interop.js");
            Instance ??= DotNetObjectReference.Create(this);

            if (_module != null)
                await _module.InvokeVoidAsync("MapOLInit", _mapId, _popupId, Defaults, Center.Value, Zoom,
                    MarkersList.Select(p => p.InternalFeature).ToArray(),
                    ShapesList.Select(p => p.InternalFeature).ToArray(),
                    LayersList.Select(p => p.InternalLayer).ToArray(),
                    Instance);

            MarkersList.CollectionChanged += MarkersOnCollectionChanged;
            ShapesList.CollectionChanged += ShapesOnCollectionChanged;
            LayersList.CollectionChanged += LayersOnCollectionChanged;
        }
    }

    [JSInvokable]
    public async Task OnInternalFeatureClick(Internal.Feature feature)
    {
#if DEBUG
        Console.WriteLine($"OnInternalFeatureClick: {JsonSerializer.Serialize(feature)}");
#endif
        await OnFeatureClick.InvokeAsync(new Feature(feature));
    }

    [JSInvokable]
    public async Task OnInternalMarkerClick(Internal.Marker marker)
    {
#if DEBUG
        Console.WriteLine($"OnInternalMarkerClick: {JsonSerializer.Serialize(marker)}");
#endif
        var m = MarkersList.FirstOrDefault(p => string.Equals(p.InternalFeature.Id.ToString(), marker.Id.ToString(), StringComparison.OrdinalIgnoreCase));

        if (m != null)
        {
            _popupContext = m;
            await OnMarkerClick.InvokeAsync(m);
            StateHasChanged();
        }
    }

    [JSInvokable]
    public async Task OnInternalShapeClick(Internal.Shape shape)
    {
#if DEBUG
        Console.WriteLine($"OnInternalShapeClick: {JsonSerializer.Serialize(shape)}");
#endif
        await OnShapeClick.InvokeAsync(shape);
        StateHasChanged();
    }

    [JSInvokable]
    public Task OnInternalClick(Coordinate coordinate)
    {
        return OnClick.InvokeAsync(coordinate);
    }

    [JSInvokable]
    public Task OnInternalPointerMove(Coordinate coordinate)
    {
        return OnPointerMove.InvokeAsync(coordinate);
    }

    [JSInvokable]
    public async Task OnInternalCenterChanged(Coordinate coordinate)
    {
        if (!coordinate.Equals(Center))
        {
            Center = coordinate;
            await CenterChanged.InvokeAsync(coordinate);
        }
    }

    [JSInvokable]
    public async Task OnInternalVisibleExtentChanged(Extent visibleExtent)
    {
        if (!visibleExtent.Equals(VisibleExtent))
        {
            VisibleExtent = visibleExtent;
            await VisibleExtentChanged.InvokeAsync(visibleExtent);
        }
    }

    [JSInvokable]
    public async Task OnInternalShapeAdded(Internal.Shape shape)
    {
#if DEBUG
        Console.WriteLine($"OnInternalShapeAdded: {JsonSerializer.Serialize(shape)}");
#endif
        if (ShapesList.All(p => p.Id != shape.Id.ToString()))
        {
            var newShape = new Shape(shape);
            ShapesList.CollectionChanged -= ShapesOnCollectionChanged;
            ShapesList.Add(newShape);
            ShapesList.CollectionChanged += ShapesOnCollectionChanged;
            await OnShapeAdded.InvokeAsync(newShape);
        }
    }

    [JSInvokable]
    public async Task OnInternalShapeChanged(Internal.Shape shape)
    {
#if DEBUG
        Console.WriteLine($"OnInternalShapeChanged: {JsonSerializer.Serialize(shape)}");
#endif
        var existingShape = ShapesList.FirstOrDefault(p => p.Id == shape.Id.ToString());

        if (existingShape == null)
        {
            await OnInternalShapeAdded(shape);
            return;
        }

        if (!existingShape.InternalFeature.Equals(shape))
        {
            existingShape.InternalFeature = shape;
            await OnShapeChanged.InvokeAsync(existingShape);
            await existingShape.OnChanged.InvokeAsync(existingShape);
        }
    }

    [JSInvokable]
    public async Task OnInternalRenderComplete()
    {
        await OnRenderComplete.InvokeAsync();
    }

    /// <summary>
    ///     Passes the center coordination to underlying map
    /// </summary>
    /// <param name="center">Center Coordinates</param>
    /// <returns>Task</returns>
    public async Task SetCenter(Coordinate center)
    {
        if (_module != null) await _module.InvokeVoidAsync("MapOLCenter", _mapId, center.Value);
    }

    [JSInvokable]
    public async Task OnInternalZoomChanged(double zoom)
    {
        Zoom = zoom;
        await ZoomChanged.InvokeAsync(zoom);
    }

    /// <summary>
    ///     Sets the zoom level to underlying map component
    /// </summary>
    /// <param name="zoom">zoom level</param>
    /// <returns></returns>
    public async Task SetZoom(double zoom)
    {
        if (_module != null) await _module.InvokeVoidAsync("MapOLZoom", _mapId, zoom);
    }

    /// <summary>
    ///     Zooms to the given extent
    /// </summary>
    /// <param name="extent"></param>
    /// <returns></returns>
    public ValueTask SetZoomToExtent(ExtentType extent)
    {
        return _module?.InvokeVoidAsync("MapOLZoomToExtent", _mapId, extent.ToString()) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Loads GeoJson data (https://geojson.org/) to the map
    /// </summary>
    /// <param name="json">GeoJson Data</param>
    /// <param name="dataProjection">Data projection of GeoJson</param>
    /// <param name="raiseEvents">Raise events for new created features and add it to list of shapes</param>
    public ValueTask LoadGeoJson(JsonElement json, string? dataProjection = null, bool? raiseEvents = true)
    {
        return _module?.InvokeVoidAsync("MapOLLoadGeoJson", _mapId, json, dataProjection, raiseEvents) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Centers the map to the current GPS geo location
    /// </summary>
    public ValueTask CenterToCurrentGeoLocation()
    {
        return _module?.InvokeVoidAsync("MapOLCenterToCurrentGeoLocation", _mapId) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Current available GPS geo location
    /// </summary>
    /// <returns>Current coordinates in the current map projection</returns>
    public ValueTask<Coordinate?> GetCurrentGeoLocation()
    {
        return _module?.InvokeAsync<Coordinate?>("MapOLGetCurrentGeoLocation", _mapId) ?? ValueTask.FromResult<Coordinate?>(null);
    }

    /// <summary>
    ///     Set given markers to underlying map component
    /// </summary>
    /// <param name="markers">collection of markers</param>
    public ValueTask SetMarkers(IEnumerable<Marker> markers)
    {
        return _module?.InvokeVoidAsync("MapOLMarkers", _mapId, markers.Select(p => p.InternalFeature).ToArray()) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Set given shapes to underlying map component
    /// </summary>
    /// <param name="shapes">collection of shapes</param>
    /// <returns></returns>
    public ValueTask SetShapes(IEnumerable<Shape> shapes)
    {
        return _module?.InvokeVoidAsync("MapOLSetShapes", _mapId, shapes.Select(p => p.InternalFeature).ToArray()) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Set all layers to underlying map component
    /// </summary>
    /// <param name="layers">collection of layers</param>
    public ValueTask SetLayers(IEnumerable<Layer> layers)
    {
        return _module?.InvokeVoidAsync("MapOLSetLayers", _mapId, layers.Select(p => p.InternalLayer).ToArray()) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Updates a single layer
    /// </summary>
    /// <param name="layer"></param>
    /// <returns></returns>
    public ValueTask UpdateLayer(Layer layer)
    {
        return _module?.InvokeVoidAsync("MapOLUpdateLayer", _mapId, layer.InternalLayer) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Set visible extent of map view
    /// </summary>
    /// <param name="extent">Extent</param>
    /// <returns></returns>
    public async Task SetVisibleExtent(Extent extent)
    {
        if (_module != null) await _module.InvokeVoidAsync("MapOLSetVisibleExtent", _mapId, extent);
    }

    [JSInvokable]
    public Task<StyleOptions> OnGetShapeStyle(Internal.Shape shape)
    {
#if DEBUG
        Console.WriteLine($"OnGetShapeStyle: {JsonSerializer.Serialize(shape)}");
#endif

        var result = ShapeStyleCallback(new Shape(shape));
        return Task.FromResult(result);
    }

    /// <summary>
    ///     Sets explicitly drawing settings
    /// </summary>
    /// <param name="newShapes"></param>
    /// <param name="editShapes"></param>
    /// <param name="shapeSnap"></param>
    /// <param name="shapeType"></param>
    /// <returns></returns>
    public async Task SetDrawingSettings(bool newShapes, bool editShapes, bool shapeSnap, ShapeType shapeType)
    {
        try
        {
            if (_module != null)
                await _module.InvokeVoidAsync("MapOLSetDrawingSettings", _mapId, newShapes, editShapes, shapeSnap, shapeType);
        }
        catch (Exception exp)
        {
            Console.Error.WriteLine("Failed to set drawing settings:\n" + exp);
        }
    }

    /// <summary>
    ///     Undo last drawing interaction
    /// </summary>
    /// <returns></returns>
    public async Task Undo()
    {
        if (_module != null) await _module.InvokeVoidAsync("MapOLUndoDrawing", _mapId);
    }

    /// <summary>
    ///     Explicit call to update an existing shape
    /// </summary>
    /// <param name="shape"></param>
    /// <returns></returns>
    public ValueTask UpdateShape(Shape shape)
    {
#if DEBUG
        Console.WriteLine($"UpdateShape: {JsonSerializer.Serialize(shape.InternalFeature)}");
#endif
        return _module?.InvokeVoidAsync("MapOLUpdateShape", _mapId, shape.InternalFeature) ?? ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Default Style Callback
    /// </summary>
    /// <param name="shape"></param>
    /// <returns></returns>
    public static StyleOptions DefaultShapeStyleCallback(Shape shape)
    {
        return new StyleOptions
        {
            Stroke = new StyleOptions.StrokeOptions
            {
                Color = "blue",
                Width = 3,
                LineDash = new double[] { 4 }
            },
            Fill = new StyleOptions.FillOptions
            {
                Color = "rgba(0, 0, 255, 0.3)"
            }
        };
    }

    private void LayersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_module == null)
            return;

        if (e.NewItems != null)
            foreach (var newLayer in e.NewItems.OfType<Layer>())
                newLayer.ParentMap = this;

        Task.Run(async () =>
        {
            if (e.OldItems != null)
                foreach (var oldLayer in e.OldItems.OfType<Layer>())
                    await _module.InvokeVoidAsync("MapOLRemoveLayer", _mapId, oldLayer.InternalLayer);

            if (e.NewItems != null)
                foreach (var newLayer in e.NewItems.OfType<Layer>())
                    await _module.InvokeVoidAsync("MapOLAddLayer", _mapId, newLayer.InternalLayer);
        });
    }

    private void ShapesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = SetShapes(ShapesList);
    }

    private void MarkersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = SetMarkers(MarkersList);
    }
}