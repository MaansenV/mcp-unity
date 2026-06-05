namespace McpUnity.Profiler
{
    public enum FrameReferenceKind
    {
        Absolute,
        Current,
        Selected,
        Latest,
        Oldest,
        RelativeToCurrent,
        RelativeToSelected,
        RelativeToLatest
    }

    public struct FrameReference
    {
        public FrameReferenceKind Kind { get; set; }
        public int Value { get; set; }

        public static FrameReference Absolute(int frameIndex) => new FrameReference { Kind = FrameReferenceKind.Absolute, Value = frameIndex };
        public static FrameReference Current() => new FrameReference { Kind = FrameReferenceKind.Current, Value = 0 };
        public static FrameReference Selected() => new FrameReference { Kind = FrameReferenceKind.Selected, Value = 0 };
        public static FrameReference Latest() => new FrameReference { Kind = FrameReferenceKind.Latest, Value = 0 };
        public static FrameReference Oldest() => new FrameReference { Kind = FrameReferenceKind.Oldest, Value = 0 };
        public static FrameReference RelativeToCurrent(int offset) => new FrameReference { Kind = FrameReferenceKind.RelativeToCurrent, Value = offset };
        public static FrameReference RelativeToSelected(int offset) => new FrameReference { Kind = FrameReferenceKind.RelativeToSelected, Value = offset };
        public static FrameReference RelativeToLatest(int offset) => new FrameReference { Kind = FrameReferenceKind.RelativeToLatest, Value = offset };
    }
}