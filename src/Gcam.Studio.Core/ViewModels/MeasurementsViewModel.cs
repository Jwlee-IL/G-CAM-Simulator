using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gcam.Studio.Core.Services;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>
/// The measurement session behind the tool picker, the image overlays and the results table: the active tool,
/// the measurements on both images, and the selected one.
/// </summary>
public sealed partial class MeasurementsViewModel : ObservableObject
{
    private ImagingResult? _result;
    private string? _reconstructionUnit;
    private int _nextNumber = 1;

    public MeasurementsViewModel()
    {
        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            ClearCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<MeasurementViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolHint))]
    private MeasureTool _activeTool = MeasureTool.Pan;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private MeasurementViewModel? _selected;

    /// <summary>How to use the active tool — shown under the tool picker.</summary>
    public string ToolHint => ActiveTool switch
    {
        MeasureTool.Distance => "Drag from one point to another.",
        MeasureTool.Angle => "Click three points; the second is the vertex. Esc cancels.",
        MeasureTool.Roi => "Drag a rectangle. Sums the pixels whose centres are inside.",
        _ => "Drag to pan. Source positions are edited in the left panel (after Reset when data exist).",
    };

    /// <summary>Adds a measurement from a finished overlay gesture and selects it.</summary>
    [RelayCommand]
    private void Add(MeasurementDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var m = new MeasurementViewModel(_nextNumber++, draft.Pane, draft.Kind, draft.PointsMm);
        Refresh(m);
        Items.Add(m);
        Selected = m;
    }

    private bool CanDelete() => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (Selected is null) return;
        int index = Items.IndexOf(Selected);
        Items.Remove(Selected);
        Selected = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasItems))]
    private void Clear()
    {
        Items.Clear();
        Selected = null;
        _nextNumber = 1;
    }

    /// <summary>Re-evaluates every measurement against a new result (ROI sums change; lengths and angles don't).</summary>
    public void Refresh(ImagingResult? result, string? reconstructionUnit = null)
    {
        _result = result;
        _reconstructionUnit = reconstructionUnit;
        foreach (var m in Items) Refresh(m);
    }

    public int ReconstructionRevision { get; private set; }
    /// <summary>Remove old-plane measurements and cancel only reconstruction drafts.</summary>
    public void ClearReconstruction()
    {
        foreach (var item in Items.Where(m => m.Pane == ImagePane.Reconstruction).ToArray()) Items.Remove(item);
        if (Selected?.Pane == ImagePane.Reconstruction) Selected = null;
        ReconstructionRevision++;
        OnPropertyChanged(nameof(ReconstructionRevision));
    }

    private void Refresh(MeasurementViewModel m)
    {
        var r = _result;
        if (m.Pane == ImagePane.Flood) m.Refresh(r?.Flood, r?.FloodOriginMm ?? 0, r?.FloodStepMm ?? 0,
            r?.StripCount is not null ? "clipped strip values" : null);
        else m.Refresh(r?.Reconstruction, r?.ReconOriginMm ?? 0, r?.ReconStepMm ?? 0, _reconstructionUnit);
    }
}
