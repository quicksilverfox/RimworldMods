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
        // Rewrite every stored state in place (used when the objects referenced by the states are recreated).
        public void Remap(System.Func<S, S> f)
        {
            for (int i = 0; i < undoMemory.Count; i++) undoMemory[i] = f(undoMemory[i]);
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
