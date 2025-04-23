using System;
using System.Collections.Generic;

/// <summary>
/// Dancing Links (DLX) implementation for solving exact-cover problems.
/// We apply it to a 25x25 Sudoku (letters A..Y) by modeling each placement
/// (row, column, digit) as a row in the exact-cover matrix with four constraints:
/// 1) Each cell must be filled (cell constraint)
/// 2) Each row must contain each digit once (row-digit constraint)
/// 3) Each column must contain each digit once (col-digit constraint)
/// 4) Each 5x5 box must contain each digit once (box-digit constraint)
///
/// Principle:
/// - Use a toroidal, doubly-linked structure of nodes and column headers.
/// - "Cover" and "Uncover" operations remove/add columns and associated rows
///   in O(1) amortized per link via pointer updates.
/// - Use a recursive depth-first search, choosing at each step the column with
///   the fewest remaining rows (heuristic for Minimum Remaining Values).  
///
/// Complexity:
/// - Building the exact-cover matrix: O(N^3) rows (~15,625) and O(N^2) columns (2,500).
/// - Search complexity is exponential in worst-case but heavily pruned by:
///     * MRV heuristic (choose smallest column)
///     * Very fast cover/uncover updates.
/// - In practice solves 25x25 Sudoku in milliseconds.
/// </summary>
public class DLX
{
    /// <summary>
    /// Node in the Dancing Links structure.
    /// Linked in four directions (L,R,U,D) and points to its column header.
    /// Also stores (Row, Col, Digit) for solution reconstruction.
    /// </summary>
    public class Node
    {
        public Node L, R, U, D;
        public Column C;
        public int Row, Col, Digit;
        public Node() { L = R = U = D = this; }
    }

    /// <summary>
    /// Column header, inherits from Node.  Tracks the number of nodes
    /// currently in the column (Size) and a Name for debugging.
    /// </summary>
    public class Column : Node
    {
        public int Size;
        public string Name;
        public Column(string name)
        {
            Name = name;
            Size = 0;
            C = this;
        }
    }

    private Column header;        // Master header node
    private Column[] columns;     // Array of all column headers
    private List<Node> solution;  // Partial solution stack

    /// <summary>
    /// Initialize DLX with a given number of columns (constraints).
    /// Creates a circular doubly-linked list of Column headers including a dummy header.
    /// </summary>
    public DLX(int colCount)
    {
        header   = new Column("header");
        columns  = new Column[colCount];
        solution = new List<Node>();

        // Link headers left-to-right in a circle
        Column prev = header;
        for (int i = 0; i < colCount; i++)
        {
            var col = new Column(i.ToString());
            columns[i] = col;

            prev.R = col;
            col.L  = prev;
            prev   = col;
        }
        prev.R     = header;
        header.L   = prev;
    }

    /// <summary>
    /// Add a row to the DLX matrix representing a possible choice.
    /// colIndices: indices of the 4 constraints this row satisfies.
    /// (row, col, digit) stored for solution reconstruction.
    /// </summary>
    public void AddRow(int[] colIndices, int row, int col, int digit)
    {
        Node first = null;
        foreach (int ci in colIndices)
        {
            var c = columns[ci];
            var nd = new Node
            {
                C     = c,
                Row   = row,
                Col   = col,
                Digit = digit
            };

            // Insert vertically at bottom of column c
            nd.D = c;
            nd.U = c.U;
            c.U.D = nd;
            c.U   = nd;
            c.Size++;

            // Link horizontally within this row
            if (first == null)
            {
                first = nd;
                nd.L   = nd;
                nd.R   = nd;
            }
            else
            {
                nd.R        = first;
                nd.L        = first.L;
                first.L.R   = nd;
                first.L     = nd;
            }
        }
    }

    /// <summary>
    /// Cover a column: remove it from the header list and also remove all rows
    /// that have a node in this column from their other columns.
    /// This enforces the exact-cover constraints.
    /// </summary>
    private void Cover(Column c)
    {
        // remove column header
        c.R.L = c.L;
        c.L.R = c.R;
        // for each row in c
        for (Node i = c.D; i != c; i = i.D)
            // for each node j in row i
            for (Node j = i.R; j != i; j = j.R)
            {
                j.D.U       = j.U;
                j.U.D       = j.D;
                j.C.Size--;
            }
    }

    /// <summary>
    /// Uncover a column: inverse of Cover.
    /// Restores the column and all its rows back into the matrix.
    /// </summary>
    private void Uncover(Column c)
    {
        for (Node i = c.U; i != c; i = i.U)
            for (Node j = i.L; j != i; j = j.L)
            {
                j.C.Size++;
                j.D.U    = j;
                j.U.D    = j;
            }
        c.R.L = c;
        c.L.R = c;
    }

    /// <summary>
    /// Recursive search for an exact cover.
    /// Returns true when all columns are covered (solution found).
    /// Uses heuristic: choose the column with fewest nodes (MRV).
    /// </summary>
    public bool Search()
    {
        // Base case: no more columns => all constraints satisfied
        if (header.R == header)
            return true;

        // Choose column with minimum size (fewest options)
        Column best = null;
        int minSize = int.MaxValue;
        for (Column c = (Column)header.R; c != header; c = (Column)c.R)
        {
            if (c.Size < minSize)
            {
                minSize = c.Size;
                best    = c;
                if (minSize == 0) break;
            }
        }

        // Cover chosen column
        Cover(best);
        // Try each row in that column
        for (Node r = best.D; r != best; r = r.D)
        {
            solution.Add(r);
            // cover all other columns in this row
            for (Node j = r.R; j != r; j = j.R)
                Cover(j.C);

            // recurse
            if (Search())
                return true;

            // backtrack: remove this row from solution
            solution.RemoveAt(solution.Count - 1);
            // uncover columns in reverse order
            for (Node j = r.L; j != r; j = j.L)
                Uncover(j.C);
        }
        // restore column
        Uncover(best);
        return false;
    }

    /// <summary>
    /// Retrieve the list of Nodes that form the solution.
    /// Each Node corresponds to one (row, col, digit) assignment.
    /// </summary>
    public List<Node> GetSolution() => solution;
}

/// <summary>
/// Wrapper for reading a 25x25 Sudoku, building DLX, solving, and outputting result.
/// </summary>
public class Solution
{
    const int N = 25;

    public static void Main()
    {
        // 1) Read input grid ('.' = empty)
        char[,] grid = new char[N, N];
        for (int i = 0; i < N; i++)
        {
            string line = Console.ReadLine();
            for (int j = 0; j < N; j++)
                grid[i, j] = line[j];
        }

        // 2) Initialize DLX: 4*N*N constraints (columns)
        int N2       = N * N;
        int colCount = 4 * N2;
        var dlx     = new DLX(colCount);

        // 3) Add rows for each cell/digit possibility
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            int b = (r / 5) * 5 + (c / 5);
            if (grid[r, c] == '.')
            {
                // empty: all 25 letters allowed
                for (int d = 0; d < N; d++)
                    AddDLXRow(dlx, r, c, b, d);
            }
            else
            {
                // pre-filled: only that letter
                int d = grid[r, c] - 'A';
                AddDLXRow(dlx, r, c, b, d);
            }
        }

        // 4) Solve exact-cover
        if (!dlx.Search())
            throw new Exception("No solution found!");

        // 5) Fill grid with solution assignments
        foreach (var node in dlx.GetSolution())
        {
            grid[node.Row, node.Col] = (char)('A' + node.Digit);
        }

        // 6) Output completed grid
        for (int i = 0; i < N; i++)
            Console.WriteLine(new string(ExtractRow(grid, i)));
    }

    /// <summary>
    /// Helper: add one DLX row for (r,c,d) covering four constraint columns.
    /// </summary>
    static void AddDLXRow(DLX dlx, int r, int c, int b, int d)
    {
        int N2      = N * N;
        int cellIdx = r * N + c;
        int rowDig  = N2     + r * N + d;
        int colDig  = 2 *N2  + c * N + d;
        int boxDig  = 3 *N2  + b * N + d;
        dlx.AddRow(new[] { cellIdx, rowDig, colDig, boxDig }, r, c, d);
    }

    /// <summary>
    /// Extract a single row from a 2D char[,] into a char[].
    /// Used for printing.
    /// </summary>
    static char[] ExtractRow(char[,] grid, int row)
    {
        char[] line = new char[N];
        for (int j = 0; j < N; j++)
            line[j] = grid[row, j];
        return line;
    }
}
