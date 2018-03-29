using System;
using System.Collections.Generic;

namespace LynnaLab
{
    // Enum of types of values that can be had.
    public enum DataValueType {
        String=0,
        HalfByte,
        Byte,
        Word,
        ByteBit,
        ByteBits,
        WordBits,
        ObjectPointer,
        WarpDestIndex,
    }

    // This class provides a way of accessing Data values of various different
    // formats.
    public class ValueReference {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Static default value stuff

        public static string[] defaultDataValues = {
            ".",
            "$0",
            "$00",
            "$0000",
            "0",
            "$00",
            "objectData4000",
            "$00",
        };

        public static void InitializeDataValues(Data data, IList<ValueReference> refs) {
            int numValues = 0;
            foreach (ValueReference r in refs) {
                if (r.valueIndex+1 > numValues)
                    numValues = r.valueIndex+1;
            }

            data.SetNumValues(numValues);

            foreach (ValueReference r in refs) {
                data.SetValue(r.valueIndex, defaultDataValues[(int)r.ValueType]);
            }
        }

        Data data;
        string constantsMappingString;
        ConstantsMapping constantsMapping;

        protected int valueIndex;
        protected int startBit,endBit;

        public DataValueType ValueType {get; set;}
        public string Name {get; set;}
        public bool Editable {get; set;}
        public Data Data { get { return data; } }

        public int MaxValue { // For integer-based ones
            get {
                switch(ValueType) {
                    case DataValueType.Byte:
                        return 0xff;
                    case DataValueType.Word:
                        return 0xffff;
                    case DataValueType.ByteBit:
                        return 1;
                    case DataValueType.ByteBits:
                    case DataValueType.WordBits:
                        return (1<<(endBit-startBit+1))-1;
                    default:
                        return 0;
                }
            }
        }
        public ConstantsMapping ConstantsMapping {
            get { return constantsMapping; }
        }
        public Project Project {
            get {return Data.Project; }
        }

        // Standard constructor for most DataValueTypes
        public ValueReference(string n, int index, DataValueType t, bool editable=true) {
            valueIndex = index;
            ValueType = t;
            Name = n;
            Editable = editable;
            if (t == DataValueType.ByteBits || t == DataValueType.ByteBit || t == DataValueType.WordBits)
                throw new Exception("Wrong constructor");
        }
        // Constructor for DataValueType.ByteBits
        public ValueReference(string n, int index, int startBit, int endBit, DataValueType t, bool editable=true) {
            valueIndex = index;
            ValueType = t;
            Name = n;
            this.startBit = startBit;
            this.endBit = endBit;
            Editable = editable;
        }
        // Constructor which takes a ConstantsMapping, affecting the interface
        // that will be used
        public ValueReference(string n, int index, int startBit, int endBit, DataValueType t, bool editable, string constantsMappingString) {
            valueIndex = index;
            ValueType = t;
            Name = n;
            this.startBit = startBit;
            this.endBit = endBit;
            Editable = editable;
            this.constantsMappingString = constantsMappingString;
        }
        // Same as above but without start/endBit
        public ValueReference(string n, int index, DataValueType t, bool editable, string constantsMappingString) {
            valueIndex = index;
            ValueType = t;
            Name = n;
            Editable = editable;
            this.constantsMappingString = constantsMappingString;
            Console.WriteLine("Mapping string " + this.constantsMappingString);
            if (t == DataValueType.ByteBits || t == DataValueType.ByteBit || t == DataValueType.WordBits)
                throw new Exception("Wrong constructor");
        }

        public ValueReference(ValueReference r) {
            data = r.data;
            valueIndex = r.valueIndex;
            ValueType = r.ValueType;
            Name = r.Name;
            startBit = r.startBit;
            endBit = r.endBit;
            Editable = r.Editable;
            constantsMappingString = r.constantsMappingString;
            constantsMapping = r.constantsMapping;
        }

        public void SetData(Data d) {
            data = d;
            if (data != null && constantsMappingString != null) {
                constantsMapping = (ConstantsMapping)typeof(Project).GetField(constantsMappingString).GetValue(data.Project);
            }
        }

        public virtual string GetStringValue() {
            return data.GetValue(valueIndex);
        }
        public virtual int GetIntValue() {
            switch(ValueType) {
                case DataValueType.ByteBits:
                case DataValueType.WordBits:
                    {
                        int andValue = (1<<(endBit-startBit+1))-1;
                        return (data.GetIntValue(valueIndex)>>startBit)&andValue;
                    }
                case DataValueType.ByteBit:
                    return (data.GetIntValue(valueIndex)>>startBit)&1;
                default:
                    return data.GetIntValue(valueIndex);
            }
        }
        public virtual void SetValue(string s) {
            switch(ValueType) {
                case DataValueType.String:
                default:
                    data.SetValue(valueIndex,s);
                    break;
            }
        }
        public virtual void SetValue(int i) {
            switch(ValueType) {
                case DataValueType.HalfByte:
                    data.SetValue(valueIndex, Wla.ToHalfByte((byte)i));
                    break;
                case DataValueType.Byte:
                case DataValueType.WarpDestIndex:
                default:
                    data.SetByteValue(valueIndex,(byte)i);
                    break;
                case DataValueType.Word:
                    data.SetWordValue(valueIndex,i);
                    break;
                case DataValueType.ByteBits:
                case DataValueType.WordBits:
                    {
                        int andValue = ((1<<(endBit-startBit+1))-1);
                        int value = data.GetIntValue(valueIndex) & (~(andValue<<startBit));
                        value |= ((i&andValue)<<startBit);
                        if (ValueType == DataValueType.ByteBits)
                            data.SetByteValue(valueIndex,(byte)value);
                        else
                            data.SetWordValue(valueIndex,value);
                    }
                    break;
                case DataValueType.ByteBit:
                    {
                        int value = data.GetIntValue(valueIndex) & ~(1<<startBit);
                        value |= ((i&1)<<startBit);
                        data.SetByteValue(valueIndex, (byte)value);
                    }
                    break;
            }
        }

        /// <summary>
        ///  Returns a field from documentation (ie. "@desc{An interaction}").
        /// </summary>
        public string GetDocumentationField(string name) {
            if (constantsMapping == null)
                return null;
            return constantsMapping.GetDocumentationField((byte)GetIntValue(), name);
        }

        /// <summary>
        ///  Used for reading subid lists. See constants/interactionTypes.s for how it's
        ///  formatted...
        ///
        ///  Each element in the list is (k,d), where k is the subid value (or just the key in
        ///  general), and d is the description.
        /// </summary>
        public IList<Tuple<string,string>> GetDocumentationFieldSubdivisions(string name) {
            if (constantsMapping == null)
                return null;
            string text = GetDocumentationField(name);
            if (text == null)
                return null;

            var output = new List<Tuple<string,string>>();

            int openBrace;
            while ((openBrace = text.IndexOf('[')) != -1) {
                int closeBrace = openBrace+1;
                int j = 1;
                while (closeBrace<text.Length && j>0) {
                    if (text[closeBrace] == '[')
                        j++;
                    if (text[closeBrace] == ']')
                        j--;
                    closeBrace++;
                }
                closeBrace--;
                if (text[closeBrace] != ']') {
                    log.Warn("Missing ']' for \"@" + name + "\"");
                    return output;
                }

                int pipe = text.IndexOf('|', openBrace);
                if (pipe == -1 || pipe > closeBrace) {
                    log.Warn("Missing ']' for \"@" + name + "\"");
                    return output;
                }

                string key = text.Substring(openBrace+1, pipe-(openBrace+1));
                string desc = text.Substring(pipe+1, closeBrace-(pipe+1));
                output.Add(new Tuple<string,string>(key,desc));

                text = text.Substring(closeBrace+1);
            }

            return output;
        }

    }
}
