using System.Collections.Generic;

namespace ResearchPowl {
    public class UndoStateHandler<S> {
        List<S> undoMemory = new List<S>();
        int currentStateIndex = -1;
        public readonly int maxStateCount = 10;

        public S Undo()
        {
            if (currentStateIndex <= 0) return default(S);
            return undoMemory[--currentStateIndex];
        }
        public S Redo()
        {
            if (currentStateIndex == undoMemory.Count - 1) return default(S);
            return undoMemory[++currentStateIndex];
        }
        public void Clear()
        {
            undoMemory.Clear();
            currentStateIndex = -1;
        }
        public bool NewState(S s)
        {
            if (currentStateIndex < undoMemory.Count - 1) undoMemory.RemoveRange(currentStateIndex + 1, undoMemory.Count - currentStateIndex - 1);
            if (undoMemory.Count == maxStateCount) undoMemory.RemoveAt(0);
            else ++currentStateIndex;
            undoMemory.Add(s);
            return true;
        }
    }
}
