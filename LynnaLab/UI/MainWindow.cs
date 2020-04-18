﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Linq;
using Gtk;
using LynnaLab;

// Doesn't extend the Gtk.Window class because the actual window is defined in Glade
// (Glade/MainWindow.ui).
public class MainWindow
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    // GUI stuff
    Gtk.Window mainWindow;

	Gtk.MenuBar menubar1;
    Gtk.MenuItem editMenuItem, actionMenuItem, debugMenuItem;

	Gtk.Notebook minimapNotebook, contextNotebook;
	Gtk.SpinButton worldSpinButton;
    Gtk.CheckButton viewObjectsCheckButton, viewWarpsCheckButton;
	Gtk.CheckButton darkenDungeonRoomsCheckbox;
	LynnaLab.HighlightingMinimap worldMinimap;
	Gtk.SpinButton dungeonSpinButton;
	Gtk.SpinButton floorSpinButton;
	LynnaLab.Minimap dungeonMinimap;

	Gtk.Statusbar statusbar1;

	LynnaLab.SpinButtonHexadecimal roomSpinButton, tilesetSpinButton;
    ComboBoxFromConstants musicComboBox;
	LynnaLab.ObjectGroupEditor objectgroupeditor1;
	LynnaLab.TilesetViewer tilesetViewer1;
	LynnaLab.RoomEditor roomeditor1;

    WarpEditor warpEditor = null;

    // Variables
    uint animationTimerID = 0;
    PluginCore pluginCore;

    Room _activeRoom;
    Map _lastActiveMap;

    // Certain "update" events are called indirectly through this. Certain updates are delayed until
    // it is "unlocked", because we sometimes don't want updates to certain widget properties to be
    // applied immediately.
    LockableEventGroup eventGroup = new LockableEventGroup();


    // Properties

    public Project Project { get; set; }
    public Room ActiveRoom {
        get {
            return _activeRoom;
        }
        set {
            if (_activeRoom != value) {
                _activeRoom = value;
                eventGroup.Invoke(OnRoomChanged);
            }
        }
    }
    public Map ActiveMap {
        get {
            return ActiveMinimap.Map;
        }
        set {
            if (ActiveMap != value && value != null) {
                if (value is Dungeon)
                    minimapNotebook.Page = 1;
                else
                    minimapNotebook.Page = 0;
                ActiveMinimap.SetMap(value);
                eventGroup.Invoke(OnMapChanged);
            }
        }
    }
    public int MapSelectedX {
        get {
            return ActiveMinimap.SelectedX;
        }
    }
    public int MapSelectedY {
        get {
            return ActiveMinimap.SelectedY;
        }
    }
    public int MapSelectedFloor {
        get {
            return ActiveMinimap.Floor;
        }
    }

    Minimap ActiveMinimap {
        get {
            return minimapNotebook.Page == 0 ? worldMinimap : dungeonMinimap;
        }
    }


    bool RoomContextActive { // "Room" tab
        get { return contextNotebook.Page == 0; }
    }
    bool ObjectContextActive { // "Objects" tab
        get { return contextNotebook.Page == 1; }
    }
    bool WarpContextActive { // "Warps" tab
        get { return contextNotebook.Page == 2; }
    }

    public MainWindow() : this("") {}
    public MainWindow (string directory)
    {
        log.Debug("Beginning Program");

        Gtk.Builder builder = new Builder();
        builder.AddFromString(Helper.ReadResourceFile("LynnaLab.Glade.MainWindow.ui"));
        builder.Autoconnect(this);

        mainWindow = (builder.GetObject("mainWindow") as Gtk.Window);
        menubar1 = (Gtk.MenuBar)builder.GetObject("menubar1");
        editMenuItem = (Gtk.MenuItem)builder.GetObject("editMenuItem");
        actionMenuItem = (Gtk.MenuItem)builder.GetObject("actionMenuItem");
        debugMenuItem = (Gtk.MenuItem)builder.GetObject("debugMenuItem");
        minimapNotebook = (Gtk.Notebook)builder.GetObject("minimapNotebook");
        contextNotebook = (Gtk.Notebook)builder.GetObject("contextNotebook");
        worldSpinButton = (Gtk.SpinButton)builder.GetObject("worldSpinButton");
        viewObjectsCheckButton = (Gtk.CheckButton)builder.GetObject("viewObjectsCheckButton");
        viewWarpsCheckButton = (Gtk.CheckButton)builder.GetObject("viewWarpsCheckButton");
        darkenDungeonRoomsCheckbox = (Gtk.CheckButton)builder.GetObject("darkenDungeonRoomsCheckbox");
        dungeonSpinButton = (Gtk.SpinButton)builder.GetObject("dungeonSpinButton");
        floorSpinButton = (Gtk.SpinButton)builder.GetObject("floorSpinButton");
        statusbar1 = (Gtk.Statusbar)builder.GetObject("statusbar1");

        roomSpinButton = new SpinButtonHexadecimal(0, 0x5ff);
        roomSpinButton.Digits = 3;
        tilesetSpinButton = new SpinButtonHexadecimal();
        tilesetSpinButton.Digits = 2;

        musicComboBox = new ComboBoxFromConstants();
        objectgroupeditor1 = new ObjectGroupEditor();
        tilesetViewer1 = new TilesetViewer();
        roomeditor1 = new RoomEditor();
        worldMinimap = new HighlightingMinimap();
        dungeonMinimap = new Minimap();
        warpEditor = new WarpEditor(this);

        ((Gtk.Box)builder.GetObject("roomSpinButtonHolder")).Add(roomSpinButton);
        ((Gtk.Box)builder.GetObject("tilesetSpinButtonHolder")).Add(tilesetSpinButton);
        ((Gtk.Box)builder.GetObject("musicComboBoxHolder")).Add(musicComboBox);
        ((Gtk.Box)builder.GetObject("objectGroupEditorHolder")).Add(objectgroupeditor1);
        ((Gtk.Box)builder.GetObject("tilesetViewerHolder")).Add(tilesetViewer1);
        ((Gtk.Box)builder.GetObject("roomEditorHolder")).Add(roomeditor1);
        ((Gtk.Box)builder.GetObject("worldMinimapHolder")).Add(worldMinimap);
        ((Gtk.Box)builder.GetObject("dungeonMinimapHolder")).Add(dungeonMinimap);
        ((Gtk.Box)builder.GetObject("warpEditorHolder")).Add(warpEditor);

        roomeditor1.Scale = 2;
        roomeditor1.TilesetViewer = tilesetViewer1;
        roomeditor1.ObjectGroupEditor = objectgroupeditor1;
        roomeditor1.WarpEditor = warpEditor;

        musicComboBox.Changed += eventGroup.Add(OnMusicComboBoxChanged);
        roomSpinButton.ValueChanged += eventGroup.Add(OnRoomSpinButtonValueChanged);
        tilesetSpinButton.ValueChanged += eventGroup.Add(OnTilesetSpinButtonValueChanged);

        worldSpinButton.ValueChanged += eventGroup.Add(OnWorldSpinButtonValueChanged);
        dungeonSpinButton.ValueChanged += eventGroup.Add(OnDungeonSpinButtonValueChanged);
        floorSpinButton.ValueChanged += eventGroup.Add(OnFloorSpinButtonValueChanged);
        minimapNotebook.SwitchPage += new SwitchPageHandler(eventGroup.Add<SwitchPageArgs>(OnMinimapNotebookSwitchPage));
        contextNotebook.SwitchPage += new SwitchPageHandler(eventGroup.Add<SwitchPageArgs>(OnContextNotebookSwitchPage));

        roomeditor1.RoomChangedEvent += eventGroup.Add<Room>((sender, room) => {
            if (room != ActiveRoom) {
                ActiveRoom = room;
                UpdateMinimapFromRoom();
            }
        });

        dungeonMinimap.AddTileSelectedHandler(eventGroup.Add<int>(delegate(object sender, int index) {
            OnMinimapTileSelected(sender, dungeonMinimap.SelectedIndex);
        }));
        worldMinimap.AddTileSelectedHandler(eventGroup.Add<int>(delegate(object sender, int index) {
            OnMinimapTileSelected(sender, worldMinimap.SelectedIndex);
        }));

        tilesetViewer1.HoverChangedEvent += eventGroup.Add<int>((sender, tile) => {
            if (tilesetViewer1.HoveringIndex == -1)
                statusbar1.Push(1,
                        "Selected Tile: 0x" + tilesetViewer1.SelectedIndex.ToString("X2"));
            else
                statusbar1.Push(1,
                        "Hovering Tile: 0x" + tilesetViewer1.HoveringIndex.ToString("X2"));
        });
        tilesetViewer1.AddTileSelectedHandler(eventGroup.Add<int>(delegate(object sender, int index) {
            statusbar1.Push(1,
                    "Selected Tile: 0x" + index.ToString("X2"));
        }));

        roomeditor1.HoverChangedEvent += eventGroup.Add<int>((sender, tile) => {
            if (roomeditor1.HoveringIndex == -1)
                statusbar1.Push(1,
                        "Selected Tile: 0x" + tilesetViewer1.SelectedIndex.ToString("X2"));
            else
                statusbar1.Push(2,
                        "Hovering Tile: (" + roomeditor1.HoveringX +
                        ", " + roomeditor1.HoveringY + ")");
        });

        worldSpinButton.Adjustment = new Adjustment(0, 0, 5, 1, 0, 0);
        dungeonSpinButton.Adjustment = new Adjustment(0, 0, 15, 1, 0, 0);

        OnDarkenDungeonRoomsCheckboxToggled(null, null);


        pluginCore = new PluginCore(this);

        LoadPlugins();

        if (directory != "")
            OpenProject(directory);

        mainWindow.ShowAll();
    }

    void LoadPlugins() {
        pluginCore.ReloadPlugins();

        foreach (Plugin plugin in pluginCore.GetPlugins()) {
            Gtk.Menu pluginSubMenu;
            if (plugin.Category == "Window")
                pluginSubMenu = editMenuItem.Submenu as Gtk.Menu;
            else if (plugin.Category == "Action")
                pluginSubMenu = actionMenuItem.Submenu as Gtk.Menu;
            else if (plugin.Category == "Debug")
                pluginSubMenu = debugMenuItem.Submenu as Gtk.Menu;
            else {
                log.Error("Unknown category '" + plugin.Category + "'.");
                continue;
            }

            var item = new MenuItem(plugin.Name);
            item.Activated += ((a, b) =>
                    {
                    plugin.Clicked();
                    });
            pluginSubMenu.Append(item);
        }
        menubar1.ShowAll();
    }

    void StartAnimations() {
        if (animationTimerID == 0)
            animationTimerID =
                GLib.Timeout.Add(1000/60, new GLib.TimeoutHandler(AnimationUpdater));
    }

    void EndAnimations() {
        if (animationTimerID != 0)
            GLib.Source.Remove(animationTimerID);
        animationTimerID = 0;
    }

    bool AnimationUpdater() {
        var tileset = tilesetViewer1.Tileset;
        if (tileset == null)
            return true;
        IList<byte> changedTiles = tileset.UpdateAnimations(1);
        return true;
    }

    void OpenProject(string dir) {
        ResponseType response = ResponseType.Yes;
        string mainFile = "ages.s";
        if (!File.Exists(dir + "/" + mainFile)) {
            Gtk.MessageDialog d = new MessageDialog(mainWindow,
                    DialogFlags.DestroyWithParent,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    "The folder you selected does not have a " + mainFile + " file. This probably indicates the folder does not contain the ages disassembly. Attempt to continue anyway?");
            response = (ResponseType)d.Run();
            d.Dispose();
        }

        if (response == ResponseType.Yes) {
            if (Project != null) {
                Project.Close();
                Project = null;
            }
            Project = new Project(dir);

            /*
            try {
                Project = new Project(dir);
            }
            catch (Exception ex) {
                string outputString = "The following error was encountered while opening the Project:\n\n";
                outputString += ex.Message;

                Gtk.MessageDialog d = new MessageDialog(this,
                                         DialogFlags.DestroyWithParent,
                                         MessageType.Error,
                                         ButtonsType.Ok,
                                         outputString);
                d.Run();
                d.Dispose();
            }
    */

            musicComboBox.SetConstantsMapping(Project.MusicMapping);
            SetWorld(0);
        }
    }

    void SetTileset(Tileset tileset) {
        if (Project == null)
            return;

        eventGroup.Lock();

        tilesetViewer1.SetTileset(tileset);
        tilesetSpinButton.Value = tileset.Index;
        ActiveRoom.SetTileset(tileset);

        eventGroup.Unlock();
    }

    void OnRoomChanged() {
        eventGroup.Lock();

        roomeditor1.SetRoom(ActiveRoom);
        roomSpinButton.Value = ActiveRoom.Index;
        warpEditor.SetMap(ActiveRoom.Index>>8, ActiveRoom.Index&0xff);
        SetTileset(ActiveRoom.Tileset);
        musicComboBox.Active = Project.MusicMapping.IndexOf((byte)ActiveRoom.GetMusicID());

        eventGroup.Unlock();
    }

    public void UpdateMinimapFromRoom() {
        eventGroup.Lock();

        int x, y, floor;
        Dungeon dungeon = Project.GetRoomDungeon(ActiveRoom, out x, out y, out floor);
        if (dungeon != null) {
            ActiveMap = dungeon;
            ActiveMinimap.Floor = floor;
            ActiveMinimap.SelectedIndex = x + y * ActiveMinimap.Width;
        }
        else {
            Room r = ActiveRoom;
            Map map = Project.GetIndexedDataType<WorldMap>(ActiveRoom.Index >> 8);
            ActiveMap = map;
            ActiveMinimap.SelectedIndex = r.Index & 0xff;
        }

        OnMapChanged();
        eventGroup.UnlockAndClear();
    }

    void OnMinimapTileSelected(object sender, int index) {
        ActiveRoom = ActiveMinimap.GetRoom();
    }

    void OnMapChanged() {
        if (_lastActiveMap == ActiveMap)
            return;
        _lastActiveMap = ActiveMap;

        eventGroup.Lock();

        ActiveMinimap.SetMap(ActiveMap);

        if (ActiveMap is Dungeon) {
            Dungeon dungeon = ActiveMap as Dungeon;
            dungeonSpinButton.Value = dungeon.Index;
            floorSpinButton.Adjustment = new Adjustment(0, 0, dungeon.NumFloors-1, 1, 0, 0);
        }
        else {
            worldSpinButton.Value = ActiveMap.Index;
        }

        ActiveRoom = ActiveMinimap.GetRoom();

        eventGroup.Unlock();
    }

    void SetWorld(WorldMap map) {
        if (Project == null)
            return;
        ActiveMap = map;
    }
    void SetWorld(int index) {
        if (Project == null)
            return;
        SetWorld(Project.GetIndexedDataType<WorldMap>(index));
    }

    // This returns ResponseType.Yes, No, or Cancel
    ResponseType AskSave(string info) {
        if (Project == null)
            return ResponseType.No;

        ResponseType response;
        Gtk.Dialog d = new Dialog("Save Project?", mainWindow,
                DialogFlags.DestroyWithParent,
                Gtk.Stock.Yes, ResponseType.Yes,
                Gtk.Stock.No, ResponseType.No,
                Gtk.Stock.Cancel, ResponseType.Cancel);
        Gtk.Label infoLabel = new Gtk.Label(info);
        infoLabel.MarginBottom = 6;
        d.ContentArea.Add(infoLabel);
        d.ShowAll();
        response = (ResponseType)d.Run();
        d.Dispose();
        if (response == ResponseType.Yes) {
            Project.Save();
        }

        return response;
    }

    void AskQuit() {
        ResponseType r = AskSave("Save project before exiting?");
        if (r == ResponseType.Yes || r == ResponseType.No)
            Quit();
    }

    void Quit() {
        if (Project != null)
            Project.Close();
        Application.Quit();
    }

    protected void OnDeleteEvent (object sender, DeleteEventArgs a)
    {
        AskQuit();
        a.RetVal = true;
    }

    protected void OnOpenActionActivated(object sender, EventArgs e)
    {
        Gtk.FileChooserDialog dialog = new FileChooserDialog("Select the ages disassembly base directory",
                mainWindow,
                FileChooserAction.SelectFolder,
                "Cancel", ResponseType.Cancel,
                "Select Folder", ResponseType.Accept);
        ResponseType response = (ResponseType)dialog.Run();

        if (response == ResponseType.Accept) {
            ResponseType r2 = AskSave("Save project before closing it?");
            if (r2 != ResponseType.Cancel) {
                string basedir = dialog.Filename;
                OpenProject(basedir);
            }
        }
        dialog.Dispose();
    }

    protected void OnSaveActionActivated(object sender, EventArgs e)
    {
        if (Project != null)
            Project.Save();
    }

    protected void OnAnimationsActionActivated(object sender, EventArgs e) {
        if ((sender as Gtk.CheckMenuItem).Active)
            StartAnimations();
        else
            EndAnimations();
    }

    protected void OnQuitActionActivated(object sender, EventArgs e)
    {
        AskQuit();
    }

    protected void OnDungeonSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        dungeonMinimap.SetMap(Project.GetIndexedDataType<Dungeon>(dungeonSpinButton.ValueAsInt));
        OnMapChanged();
    }

    protected void OnFloorSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        dungeonMinimap.Floor = (sender as Gtk.SpinButton).ValueAsInt;
        OnMapChanged();
    }

    protected void OnWorldSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        WorldMap world = Project.GetIndexedDataType<WorldMap>(worldSpinButton.ValueAsInt);
        if (ActiveMap != world)
            SetWorld(world);
    }

    protected void OnMinimapNotebookSwitchPage(object o, SwitchPageArgs args)
    {
        if (Project == null)
            return;
        Notebook nb = minimapNotebook;
        if (nb.Page == 0)
            ActiveMinimap.SetMap(Project.GetIndexedDataType<WorldMap>(worldSpinButton.ValueAsInt));
        else if (nb.Page == 1)
            ActiveMinimap.SetMap(Project.GetIndexedDataType<Dungeon>(dungeonSpinButton.ValueAsInt));
        OnMapChanged();
    }

    protected void OnContextNotebookSwitchPage(object o, SwitchPageArgs args)
    {
        UpdateRoomEditorViews();
    }

    void UpdateRoomEditorViews() {
        bool viewObjects = viewObjectsCheckButton.Active;
        bool viewWarps = viewWarpsCheckButton.Active;

        roomeditor1.EnableTileEditing = RoomContextActive;

        if (ObjectContextActive) { // Object editor
            viewObjects = true;
        }
        if (WarpContextActive) { // Warp editor
            viewWarps = true;
        }

        roomeditor1.ViewObjects = viewObjects;
        roomeditor1.ViewWarps = viewWarps;
    }

    protected void OnRoomSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        SpinButton button = sender as SpinButton;
        Room r = Project.GetIndexedDataType<Room>(button.ValueAsInt);
        if (r != ActiveRoom) {
            ActiveRoom = r;
            UpdateMinimapFromRoom();
        }
    }

    protected void OnTilesetSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        SpinButton button = sender as SpinButton;
        SetTileset(Project.GetIndexedDataType<Tileset>(button.ValueAsInt));
    }


    protected void OnMusicComboBoxChanged(object sender, EventArgs e) {
        if (Project == null)
            return;
        ActiveRoom.SetMusicID(Project.MusicMapping.StringToByte(musicComboBox.ActiveId));
    }

    protected void OnTilesetEditorButtonClicked(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        Window win = new Window(WindowType.Toplevel);
        TilesetEditor a = new TilesetEditor(tilesetViewer1.Tileset);
        win.Add(a);
        win.Title = "Edit Tileset";
        win.ShowAll();
    }

    protected void OnViewObjectsCheckBoxToggled(object sender, EventArgs e)
    {
        roomeditor1.ViewObjects = (sender as Gtk.CheckButton).Active;
        roomeditor1.QueueDraw();
    }

    protected void OnViewWarpsCheckBoxToggled(object sender, EventArgs e)
    {
        roomeditor1.ViewWarps = (sender as Gtk.CheckButton).Active;
        roomeditor1.QueueDraw();
    }

    protected void OnDarkenDungeonRoomsCheckboxToggled(object sender, EventArgs e)
    {
        worldMinimap.DarkenUsedDungeonRooms = darkenDungeonRoomsCheckbox.Active;
    }

    void OnWindowClosed(object sender, DeleteEventArgs e) {
        AskQuit();
        e.RetVal = true; // Event is "handled". This prevents the window closure.
    }
}
