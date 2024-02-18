﻿using Dalamud.Plugin.Services;
using Glamourer.Interop.Structs;
using ImGuiNET;
using OtterGui.Services;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;

namespace Glamourer.Interop.Material;

public sealed unsafe class LiveColorTablePreviewer : IService, IDisposable
{
    private readonly IObjectTable _objects;
    private readonly IFramework   _framework;

    public  MaterialValueIndex  LastValueIndex         { get; private set; } = MaterialValueIndex.Invalid;
    public  MtrlFile.ColorTable LastOriginalColorTable { get; private set; }
    private MaterialValueIndex  _valueIndex      = MaterialValueIndex.Invalid;
    private ObjectIndex         _lastObjectIndex = ObjectIndex.AnyIndex;
    private ObjectIndex         _objectIndex     = ObjectIndex.AnyIndex;
    private MtrlFile.ColorTable _originalColorTable;


    public LiveColorTablePreviewer(IObjectTable objects, IFramework framework)
    {
        _objects          =  objects;
        _framework        =  framework;
        _framework.Update += OnFramework;
    }

    private void Reset()
    {
        if (LastValueIndex.DrawObject is MaterialValueIndex.DrawObjectType.Invalid || _lastObjectIndex == ObjectIndex.AnyIndex)
            return;

        var actor = (Actor)_objects.GetObjectAddress(_lastObjectIndex.Index);
        if (actor.IsCharacter && LastValueIndex.TryGetTexture(actor, out var texture))
            MaterialService.ReplaceColorTable(texture, LastOriginalColorTable);

        LastValueIndex   = MaterialValueIndex.Invalid;
        _lastObjectIndex = ObjectIndex.AnyIndex;
    }

    private void OnFramework(IFramework _)
    {
        if (_valueIndex.DrawObject is MaterialValueIndex.DrawObjectType.Invalid || _objectIndex == ObjectIndex.AnyIndex)
        {
            Reset();
            _valueIndex  = MaterialValueIndex.Invalid;
            _objectIndex = ObjectIndex.AnyIndex;
            return;
        }

        var actor = (Actor)_objects.GetObjectAddress(_objectIndex.Index);
        if (!actor.IsCharacter)
        {
            _valueIndex  = MaterialValueIndex.Invalid;
            _objectIndex = ObjectIndex.AnyIndex;
            return;
        }

        if (_valueIndex != LastValueIndex || _lastObjectIndex != _objectIndex)
        {
            Reset();
            LastValueIndex         = _valueIndex;
            _lastObjectIndex       = _objectIndex;
            LastOriginalColorTable = _originalColorTable;
        }

        if (_valueIndex.TryGetTexture(actor, out var texture))
        {
            var diffuse  = CalculateDiffuse();
            var emissive = diffuse / 8;
            var table    = LastOriginalColorTable;
            if (_valueIndex.RowIndex != byte.MaxValue)
            {
                table[_valueIndex.RowIndex].Diffuse  = diffuse;
                table[_valueIndex.RowIndex].Emissive = emissive;
            }
            else
            {
                
                for (var i = 0; i < MtrlFile.ColorTable.NumRows; ++i)
                {
                    table[i].Diffuse = diffuse;
                    table[i].Emissive = emissive;
                }
            }

            MaterialService.ReplaceColorTable(texture, table);
        }

        _valueIndex  = MaterialValueIndex.Invalid;
        _objectIndex = ObjectIndex.AnyIndex;
    }

    public void OnHover(MaterialValueIndex index, ObjectIndex objectIndex, MtrlFile.ColorTable table)
    {
        if (_valueIndex.DrawObject is not MaterialValueIndex.DrawObjectType.Invalid)
            return;

        _valueIndex  = index;
        _objectIndex = objectIndex;
        if (LastValueIndex.DrawObject is MaterialValueIndex.DrawObjectType.Invalid
         || _lastObjectIndex == ObjectIndex.AnyIndex
         || LastValueIndex.MaterialIndex != _valueIndex.MaterialIndex
         || LastValueIndex.DrawObject != _valueIndex.DrawObject
         || LastValueIndex.SlotIndex != _valueIndex.SlotIndex)
            _originalColorTable = table;
    }

    private static Vector3 CalculateDiffuse()
    {
        const int frameLength = 1;
        const int steps       = 64;
        var       frame       = ImGui.GetFrameCount();
        var       hueByte     = frame % (steps * frameLength) / frameLength;
        var       hue         = (float)hueByte / steps;
        ImGui.ColorConvertHSVtoRGB(hue, 1, 1, out var r, out var g, out var b);
        return new Vector3(r, g, b);
    }

    public void Dispose()
    {
        Reset();
        _framework.Update -= OnFramework;
    }
}