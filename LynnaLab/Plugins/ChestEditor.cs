using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using LynnaLab;
using Gtk;

namespace Plugins
{
    public class ChestEditor : Plugin
    {
        PluginManager manager;

        public override String Name { get { return "Chest Editor"; } }
        public override String Tooltip { get { return "Edit Chests"; } }
        public override bool IsDockable { get { return false; } }
        public override string Category { get { return "Window"; } }

        Project Project {
            get {
                return manager.Project;
            }
        }

        // Methods

        public override void Init(PluginManager manager) {
            this.manager = manager;
        }

        public override void Exit() {
        }

        public override void Clicked() {
            Treasure.Project = Project;
            Chest.Project = Project;

            Gtk.Window win = new Window(WindowType.Toplevel);
            Alignment warningsContainer = new Alignment(0.1f,0.1f,0f,0f);

            VBox vbox = new VBox();

            var chestGui = new ChestEditorGui(manager);
            chestGui.SetRoom(manager.GetActiveRoom().Index);
            chestGui.Destroyed += (sender2, e2) => win.Destroy();

            Frame chestFrame = new Frame();
            chestFrame.Label = "Chest Data";
            chestFrame.Add(chestGui);

            var treasureGui = new TreasureEditorGui(manager);
            Frame treasureFrame = new Frame();
            treasureFrame.Label = "Treasure Data";
            treasureFrame.Add(treasureGui);

            System.Action UpdateWarnings = () => {
                VBox warningBox = new VBox();
                warningBox.Spacing = 4;

                System.Action<string> AddWarning = (s) => {
                    Image img = new Image(Stock.DialogWarning, IconSize.Button);
                    HBox hb = new HBox();
                    hb.Spacing = 10;
                    hb.Add(img);
                    Gtk.Label l = new Gtk.Label(s);
                    l.LineWrap = true;
                    hb.Add(l);
                    Alignment a = new  Alignment(0,0,0,0);
                    a.Add(hb);
                    warningBox.Add(a);
                };

                foreach (var c in warningsContainer.Children)
                    warningsContainer.Remove(c);

                int index = chestGui.GetTreasureIndex();
                if (index < 0)
                    return;

                if (!Treasure.IndexExists(index)) {
                    AddWarning("Treasure " + Wla.ToWord(index) + " does not exist.");
                }
                else {
                    if (index != treasureGui.Index)
                        AddWarning("Your treasure index is different\nfrom the chest you're editing.");

                    int spawnMode = (Treasure.GetTreasureByte(index, 0) >> 4)&7;

                    if (spawnMode != 3) {
                        AddWarning("Treasure " + Wla.ToWord(index) + " doesn't have spawn\nmode $3 (needed for chests).");
                    }

                    int yx = Chest.GetChestByte(chestGui.RoomIndex, 0);
                    int x=yx&0xf;
                    int y=yx>>4;
                    Room r = Project.GetIndexedDataType<Room>(chestGui.RoomIndex);
                    if (x >= r.Width || y >= r.Height || r.GetTile(x,y) != 0xf1) {
                        AddWarning("There is no chest at coordinates (" + x + "," + y + ").");
                    }
                }

                warningsContainer.Add(warningBox);

                win.ShowAll();
            };

            chestGui.SetTreasureEditor(treasureGui);
            chestGui.ChestChangedEvent += () => {
                UpdateWarnings();
            };
            treasureGui.TreasureChangedEvent += () => {
                UpdateWarnings();
            };

            HBox hbox = new Gtk.HBox();
            hbox.Spacing = 6;
            hbox.Add(chestFrame);
            hbox.Add(treasureFrame);

            Button okButton = new Gtk.Button();
            okButton.UseStock = true;
            okButton.Label = "gtk-ok";
            okButton.Clicked += (a,b) => {
                win.Destroy();
            };

            Alignment buttonAlign = new Alignment(0.5f,0.5f,0f,0f);
            buttonAlign.Add(okButton);

            vbox.Add(hbox);
            vbox.Add(warningsContainer);
            vbox.Add(buttonAlign);

            win.Add(vbox);

            UpdateWarnings();
            win.ShowAll();
        }

    }

    class TreasureEditorGui : Gtk.Alignment {
        public event System.Action TreasureChangedEvent;

        PluginManager manager;
        Widget vrEditor;
        Alignment vrContainer;
        SpinButtonHexadecimal highIndexButton, lowIndexButton;

        Project Project {
            get { return manager.Project; }
        }

        public int Index {
            get {
                return highIndexButton.ValueAsInt<<8 | lowIndexButton.ValueAsInt;
            }
        }

        public TreasureEditorGui(PluginManager manager)
            : base(0.5f,0.0f,0.0f,0.0f)
        {
            this.manager = manager;

            highIndexButton = new SpinButtonHexadecimal(0,0xff);
            highIndexButton.Digits = 2;
            highIndexButton.ValueChanged += (a,b) => {
                SetTreasure(Index);
            };

            Button hAddButton = new Gtk.Button();
            hAddButton.Clicked += (a,b) => {
                Treasure.AddHighIndex();
                SetTreasure(0xffff);
            };
			hAddButton.UseStock = true;
			hAddButton.UseUnderline = true;
            hAddButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);

            Button hRemoveButton = new Gtk.Button();
            hRemoveButton.Clicked += (a,b) => {
                Gtk.MessageDialog d = new MessageDialog(null,
                        DialogFlags.DestroyWithParent,
                        MessageType.Warning,
                        ButtonsType.YesNo,
                        "This will shift the indices for all treasures starting from " + 
                        Wla.ToByte((byte)(Index>>8)) + "! All treasures after this WILL BREAK! " +
                        "Are you sure you want to continue?"
                        );
                var r = (ResponseType)d.Run();
                d.Destroy();
                if (r != Gtk.ResponseType.Yes)
                    return;
                Treasure.RemoveHighIndex(Index>>8);
                SetTreasure(Index);
            };
			hRemoveButton.UseStock = true;
			hRemoveButton.UseUnderline = true;
            hRemoveButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);

            lowIndexButton = new SpinButtonHexadecimal(0,0xff);
            lowIndexButton.Digits = 2;
            lowIndexButton.ValueChanged += (a,b) => {
                SetTreasure(Index);
            };

            Button addButton = new Gtk.Button();
            addButton.Clicked += (a,b) => {
                Treasure.AddSubIndex(Index>>8);
                SetTreasure((Index&0xff00) + 0xff);
            };
			addButton.UseStock = true;
			addButton.UseUnderline = true;
            addButton.Image = new Gtk.Image(Gtk.Stock.Add, Gtk.IconSize.Button);

            Button removeButton = new Gtk.Button();
            removeButton.Clicked += (a,b) => {
                if ((Index&0xff) < Treasure.GetNumLowIndices(Index>>8)-1) {
                    Gtk.MessageDialog d = new MessageDialog(null,
                            DialogFlags.DestroyWithParent,
                            MessageType.Warning,
                            ButtonsType.YesNo,
                            "This will shift all sub-indices for treasure " +
                            Wla.ToByte((byte)(Index>>8)) + " starting from sub-index " +
                            Wla.ToByte((byte)(Index&0xff)) + "! Are you sure you want to continue?"
                            );
                    var r = (ResponseType)d.Run();
                    d.Destroy();
                    if (r != Gtk.ResponseType.Yes)
                        return;
                }
                Treasure.RemoveSubIndex(Index);
                SetTreasure((Index&0xff00) + 0xff);
            };
			removeButton.UseStock = true;
			removeButton.UseUnderline = true;
            removeButton.Image = new Gtk.Image(Gtk.Stock.Remove, Gtk.IconSize.Button);

            var table = new Table(3,2,false);

            uint y=0;
            table.Attach(new Gtk.Label("ID1"), 0, 1, y, y+1);
            table.Attach(highIndexButton, 1, 2, y, y+1);
            // Disable high add and remove buttons for now, they're not useful
            // yet
//             table.Attach(hAddButton,2,3,y,y+1);
//             table.Attach(hRemoveButton,3,4,y,y+1);
            y++;
            table.Attach(new Gtk.Label("ID2"), 0,1,y,y+1);
            table.Attach(lowIndexButton, 1,2,y,y+1);
            table.Attach(addButton,2,3,y,y+1);
            table.Attach(removeButton,3,4,y,y+1);

            vrContainer = new Alignment(1.0f,1.0f,1.0f,1.0f);

            VBox vbox = new VBox();
            vbox.Add(table);
            vbox.Add(vrContainer);

            Add(vbox);

            SetTreasure(0);
        }

        public void SetTreasure(int index) {
            int hIndex = index>>8;
            int lIndex = index&0xff;

            int hMax = Treasure.GetNumHighIndices();
            if (hIndex >= hMax)
                hIndex = hMax-1;

            int lMax = Treasure.GetNumLowIndices(hIndex);
            if (lIndex >= lMax)
                lIndex = lMax-1;

            highIndexButton.Adjustment.Upper = hMax-1;
            lowIndexButton.Adjustment.Upper = lMax-1;
            highIndexButton.Value = hIndex;
            lowIndexButton.Value = lIndex;

            index = hIndex<<8|lIndex;

            vrContainer.Remove(vrEditor);

            Data data = Treasure.GetTreasureDataBase(index);
            DataValueReference v1 = new DataValueReference(data, "Spawn Mode", 0, DataValueType.ByteBits, 4,6);
            DataValueReference v5 = new DataValueReference(data, "Grab Mode", 0, DataValueType.ByteBits, 0,2);
            DataValueReference v6 = new DataValueReference(data, "Unknown", 0, DataValueType.ByteBit, 3,3);

            data = data.NextData;
            DataValueReference v2 = new DataValueReference(data, "Parameter", 0, DataValueType.Byte);
            data = data.NextData;
            DataValueReference v3 = new DataValueReference(data, "Text ID", 0, DataValueType.Byte);
            data = data.NextData;
            DataValueReference v4 = new DataValueReference(data, "Gfx", 0, DataValueType.Byte);
            data = data.NextData;

            // Byte 1 is sometimes set to 0x80 for unused treasures?
            v1.SetValue(v1.GetIntValue()&0x7f);

            ValueReferenceGroup vrGroup = new ValueReferenceGroup(new DataValueReference[] {v1, v5, v6, v2, v3, v4});

            var vr = new ValueReferenceEditor(
                    Project,
                    vrGroup,
                    "Data");
            vr.SetMaxBound(0, 0x7f); // Max bound for Spawn Mode

            vr.AddDataModifiedHandler(() => {
                    if (TreasureChangedEvent != null)
                        TreasureChangedEvent();
                });

            vrEditor = vr;
            vrContainer.Add(vrEditor);

            if (TreasureChangedEvent != null)
                TreasureChangedEvent();
        }
    }

    static class Treasure {
        public static Project Project { get; set; }

        public static void AddSubIndex(int hIndex) {
            int n = GetNumLowIndices(hIndex);
            if (n >= 256)
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            if (!HighIndexUsesPointer(hIndex)) {
                string pointerString = "treasureObjectData" + hIndex.ToString("x2");

                while (Project.HasLabel(pointerString))
                    pointerString = "_" + pointerString;

                parser.InsertParseableTextBefore(data,
                        new string[] {
                        "\t.db $80",
                        "\t.dw " + pointerString,
                        "\t.db $00",
                        });

                string output = "\t.db";

                for (int i=0;i<4;i++) {
                    output += " " + data.GetValue(0);
                    Data d2 = data;
                    data = data.NextData;
                    parser.RemoveFileComponent(d2);
                }


                parser.InsertParseableTextAfter(null,
                        new string[] {
                        pointerString + ":"
                        });

                parser.InsertParseableTextAfter(null,
                        new string[] { output });

                data = parser.GetData(pointerString);
            }
            else {
                data = Project.GetData(data.NextData.GetValue(0));
            }

            for (int i=0;i<(n-1)*4+3;i++)
                data = data.NextData;

            parser.InsertParseableTextAfter(data,
                    new string[] {
                    "\t.db $00 $00 $00 $00",
                    });
        }

        public static void RemoveSubIndex(int index) {
            int hIndex = index>>8;
            int lIndex = index&0xff;

            int n = GetNumLowIndices(hIndex);
            if (n <= 1)
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            data = Project.GetData(data.NextData.GetValue(0));

            for (int i=0;i<lIndex*4;i++)
                data = data.NextData;

            for (int i=0;i<4;i++) {
                Data d2 = data;
                data = data.NextData;
                parser.RemoveFileComponent(d2);
            }
        }

        public static void AddHighIndex() {
            int max = GetNumHighIndices();
            Data data = GetHighIndexDataBase(max-1);
            FileParser parser = data.FileParser;

            int n = 4;
            if (HighIndexUsesPointer(max-1))
                n = 3;
            for (int i=0;i<n-1;i++)
                data = data.NextData;

            parser.InsertParseableTextAfter(data,
                    new string[] {
                    "",
                    "\t.db $00 $00 $00 $00",
                    });
        }

        public static void RemoveHighIndex(int hIndex) {
            if (hIndex >= GetNumHighIndices())
                return;

            Data data = GetHighIndexDataBase(hIndex);
            FileParser parser = data.FileParser;

            string pointerString = "";
            int n=4;
            if (HighIndexUsesPointer(hIndex)) {
                pointerString = data.NextData.GetValue(0);
                n=3;
            }

            for (int i=0;i<n;i++) {
                Data d2 = data;
                data = data.NextData;

                parser.RemoveFileComponent(d2);
            }

            if (pointerString != "") {
                data = parser.GetData(pointerString);
                LynnaLab.Label l = parser.GetDataLabel(data);
                parser.RemoveFileComponent(l);
                do {
                    Data d2 = data;
                    data = data.NextData;
                    parser.RemoveFileComponent(d2);
                }
                while (data != null && parser.GetDataLabel(data) == null);
            }
        }

        public static int GetNumHighIndices() {
            // Read until the first label after treasureData.
            FileParser parser = Project.GetFileWithLabel("treasureObjectData");
            Data data = parser.GetData("treasureObjectData");

            int count=0;
            do {
                for (int i=0; i<4;) {
                    i += data.Size;
                    data = data.NextData;
                }
                count++;
            }
            while (data != null && parser.GetDataLabel(data) == null);

            return count;
        }

        static Data GetHighIndexDataBase(int hIndex) {
            return Project.GetData("treasureObjectData", hIndex*4);
        }

        public static bool HighIndexUsesPointer(int hIndex) {
            Data data = GetHighIndexDataBase(hIndex);
            if ((data.GetIntValue(0) & 0x80) == 0)
                return false;
            return data.NextData.CommandLowerCase == ".dw";
        }

        public static int GetNumLowIndices(int hIndex) {
            Data data = GetHighIndexDataBase(hIndex);
            if (!HighIndexUsesPointer(hIndex))
                return 1;
            data = data.NextData;
            FileParser parser = Project.GetFileWithLabel(data.GetValue(0));
            data = parser.GetData(data.GetValue(0));
            int count=0;
            do {
                for (int i=0;i<4;i++)
                    data = data.NextData;
                count++;
            }
            while (data != null && parser.GetDataLabel(data) == null);

            return count;
        }

        public static Data GetTreasureDataBase(int treasure) {
            int hIndex = treasure>>8;
            int lIndex = treasure&0xff;

            if (hIndex >= GetNumHighIndices() || lIndex >= GetNumLowIndices(hIndex)) {
                return null;
            }

            Data data = GetHighIndexDataBase(hIndex);

            if (!HighIndexUsesPointer(hIndex)) {
                if (lIndex != 0)
                    throw new Exception();
                return data;
            }

            data = Project.GetData(data.NextData.GetValue(0));
            for (int i=0;i<lIndex*4;i++)
                data = data.NextData;
            return data;
        }

        public static int GetTreasureByte(int treasure, int byteIndex) {
            Data data = GetTreasureDataBase(treasure);

            for (int i=0;i<byteIndex;i++)
                data = data.NextData;
            return data.GetIntValue(0);
        }

        public static bool IndexExists(int treasure) {
            Data data = GetTreasureDataBase(treasure);
            return data != null;
        }
    }

    class ChestEditorGui : Gtk.Alignment {
        public event System.Action ChestChangedEvent;

        PluginManager manager;
        Widget vrEditor;
        Alignment vrContainer;
        SpinButtonHexadecimal indexSpinButton;

        TreasureEditorGui friend;

        DataValueReference v1,v2,v3,v4;

        public int RoomIndex {
            get {
                return indexSpinButton.ValueAsInt;
            }
        }

        Project Project {
            get {
                return manager.Project;
            }
        }

        public ChestEditorGui(PluginManager m)
            : base(1.0F,0.0F,1.0F,0.0F)
        {
            manager = m;

            indexSpinButton = new SpinButtonHexadecimal(0,Project.NumRooms-1);
            indexSpinButton.Digits = 3;
            indexSpinButton.ValueChanged += (a,b) => {
                SetRoom(indexSpinButton.ValueAsInt);
            };

            HBox roomIndexBox = new HBox();
            roomIndexBox.Add(new Gtk.Label("Room"));
            roomIndexBox.Add(indexSpinButton);

            VBox vbox = new VBox();
            vbox.Add(roomIndexBox);

            vrContainer = new Alignment(1.0F, 1.0F, 1.0F, 1.0F);
            vbox.Add(vrContainer);

            Alignment buttonAlign = new Alignment(0.5F, 0.5F, 0.0F, 0.2F);
            buttonAlign.TopPadding = 3;

            Button syncButton = new Button("Sync Treasure Data");
            syncButton.Clicked += (a,b) => {
                SyncTreasureEditor();
            };
            buttonAlign.Add(syncButton);

            vbox.Add(buttonAlign);

            Add(vbox);
        }

        public int GetTreasureIndex() {
            if (v3 == null)
                return -1;
            return v3.GetIntValue()<<8 | v4.GetIntValue();
        }

        void SyncTreasureEditor() {
            if (friend != null && v3 != null) {
                friend.SetTreasure(v3.GetIntValue()<<8 | v4.GetIntValue());
            }
        }

        public void SetTreasureEditor(TreasureEditorGui gui) {
            friend = gui;

            SyncTreasureEditor();
        }

        public void SetRoom(int room) {
            indexSpinButton.Value = room;

            vrContainer.Remove(vrEditor);
            vrEditor = null;
            v1 = null;
            v2 = null;
            v3 = null;
            v4 = null;

            Data data = Chest.GetChestData(room);

            if (data == null) {
                VBox vbox = new VBox();

                Button addButton = new Button("Add");
                addButton.Clicked += (a,b) => {
                    Chest.AddChestData(RoomIndex);
                    SetRoom(RoomIndex);
                };
                addButton.Label = "gtk-add";
                addButton.UseStock = true;

                var l = new Gtk.Label("No chest data\nexists for this room.");
                vbox.Add(l);
                var btnAlign = new Alignment(0.5f,0.5f,0.0f,0.2f);
                btnAlign.TopPadding = 3;
                btnAlign.Add(addButton);
                vbox.Add(btnAlign);

                vrEditor = vbox;
            }
            else {

                v1 = new DataValueReference(data, "YX", 0, DataValueType.Byte);
                data = data.NextData;
                v2 = new DataValueReference(data, "Room", 0, DataValueType.Byte, editable:false);
                data = data.NextData;
                v3 = new DataValueReference(data, "ID1", 0, DataValueType.Byte);
                data = data.NextData;
                v4 = new DataValueReference(data, "ID2", 0, DataValueType.Byte);
                data = data.NextData;

                ValueReferenceGroup vrGroup = new ValueReferenceGroup(new DataValueReference[] {v1, v2, v3, v4});

                var vr = new ValueReferenceEditor(
                        Project,
                        vrGroup,
                        "Data");
                vr.SetMaxBound(0, 0xfe); // Max bound for YX

                vr.AddDataModifiedHandler(() => {
                        if (ChestChangedEvent != null)
                            ChestChangedEvent();
                        });

                VBox vbox = new VBox();
                vbox.Add(vr);

                Button delButton = new Button("Remove");
                delButton.Clicked += (a,b) => {
                    Chest.DeleteChestData(RoomIndex);
                    SetRoom(RoomIndex);
                };
                delButton.Label = "gtk-delete";
                delButton.UseStock = true;

                Alignment btnAlign = new Alignment(0.5f,0.5f,0.0f,0.2f);
                btnAlign.TopPadding = 3;
                btnAlign.Add(delButton);
                vbox.Add(btnAlign);

                vrEditor = vbox;
            }

            if (ChestChangedEvent != null)
                ChestChangedEvent();

            vrContainer.Add(vrEditor);
            vrContainer.ShowAll();
        }

    }

    class Chest {
        public static Project Project { get; set; }

        public static Data GetChestData(int room) {
            int group = room>>8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            while (chestGroupData.GetIntValue(0) != 0xff) {
                if (chestGroupData.NextData.GetIntValue(0) == room)
                    return chestGroupData;
                for (int i=0;i<4;i++)
                    chestGroupData = chestGroupData.NextData;
            }

            return null;
        }

        public static void AddChestData(int room) {
            int group = room>>8;
            room &= 0xff;

            FileParser chestFileParser = Project.GetFileWithLabel("chestDataGroupTable");
            Data chestPointer = chestFileParser.GetData("chestDataGroupTable", group*2);
            string pointerString = chestPointer.GetValue(0);
            Data chestGroupData = Project.GetData(pointerString);

            Data newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, new List<string>{"\t"});
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {Wla.ToByte((byte)room)}, -1, null, null);
            newData.PrintCommand = false;
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, null);
            newData.PrintCommand = false;
            newData.EndsLine = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);

            newData = new Data(Project, ".db", new string[] {"$00"}, -1, null, null);
            newData.PrintCommand = false;
            chestFileParser.InsertComponentBefore(chestGroupData, newData);
        }

        public static void DeleteChestData(int room) {
            Data data = GetChestData(room);

            if (data == null)
                return;

            for (int i=0;i<4;i++) {
                Data nextData = data.NextData;
                data.FileParser.RemoveFileComponent(data);
                data = nextData;
            }
        }

        public static int GetChestByte(int room, int index) {
            Data data = GetChestData(room);
            if (data == null)
                return -1;

            for (int i=0;i<index;i++)
                data = data.NextData;
            return data.GetIntValue(0);
        }
    }
}
