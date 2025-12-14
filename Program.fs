namespace LibrarySystem

open System
open System.Data.SQLite
open Dapper
open System.IO
open System.Windows.Forms
open System.Drawing

// ==========================================
// 1. DOMAIN MODEL
// ==========================================
[<CLIMutable>]
type Book = {
    Id: int
    Title: string
    Author: string
    ISBN: string
    TotalCopies: int
    AvailableCopies: int
}

// ==========================================
// 2. BACKEND OPERATIONS
// ==========================================
module Backend =
    let dbFile = "Library.db"
    let connectionString = sprintf "Data Source=%s;Version=3;" dbFile

    let initializeDatabase () =
        if not (File.Exists dbFile) then SQLiteConnection.CreateFile(dbFile)
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Open()
            let sql = """
            CREATE TABLE IF NOT EXISTS Books (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Author TEXT NOT NULL,
                ISBN TEXT NOT NULL UNIQUE, 
                TotalCopies INTEGER DEFAULT 1,
                AvailableCopies INTEGER DEFAULT 1
            );
            """
            conn.Execute(sql) |> ignore
        )

    let getAllBooks () =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Query<Book>("SELECT * FROM Books") |> Seq.toList
        )

    let addOrUpdateBook (title: string) (author: string) (isbn: string) =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Open()
            let checkSql = "SELECT count(*) FROM Books WHERE ISBN = @ISBN"
            let count = conn.ExecuteScalar<int>(checkSql, {| ISBN = isbn |})

            if count > 0 then
                let updateSql = "UPDATE Books SET TotalCopies = TotalCopies + 1, AvailableCopies = AvailableCopies + 1 WHERE ISBN = @ISBN"
                conn.Execute(updateSql, {| ISBN = isbn |}) |> ignore
                "Existing book updated! Added a new copy."
            else
                let insertSql = "INSERT INTO Books (Title, Author, ISBN, TotalCopies, AvailableCopies) VALUES (@Title, @Author, @ISBN, 1, 1)"
                conn.Execute(insertSql, {| Title = title; Author = author; ISBN = isbn |}) |> ignore
                "New book created successfully!"
        )

    // === وظيفة الحذف الجديدة ===
    let deleteBook (id: int) =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Open()
            let sql = "DELETE FROM Books WHERE Id = @Id"
            conn.Execute(sql, {| Id = id |}) |> ignore
            "Book deleted permanently."
        )

    // === وظيفة تعديل البيانات الجديدة ===
    let updateBookDetails (id: int) (title: string) (author: string) (isbn: string) =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Open()
            try
                let sql = "UPDATE Books SET Title = @Title, Author = @Author, ISBN = @ISBN WHERE Id = @Id"
                conn.Execute(sql, {| Id = id; Title = title; Author = author; ISBN = isbn |}) |> ignore
                "Success: Book details updated."
            with
            | _ -> "Error: Check if ISBN already exists for another book."
        )

    let borrowBook (id: int) =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Open()
            let sql = "UPDATE Books SET AvailableCopies = AvailableCopies - 1 WHERE Id = @Id AND AvailableCopies > 0"
            let affected = conn.Execute(sql, {| Id = id |})
            if affected > 0 then "Book Borrowed" else "No copies available!"
        )

    let returnBook (id: int) =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            conn.Open()
            let sql = "UPDATE Books SET AvailableCopies = AvailableCopies + 1 WHERE Id = @Id AND AvailableCopies < TotalCopies"
            let affected = conn.Execute(sql, {| Id = id |})
            if affected > 0 then "Book Returned" else "All copies returned!"
        )
    
    let searchBooks (query: string) =
        using (new SQLiteConnection(connectionString)) (fun conn ->
            let sql = "SELECT * FROM Books WHERE Title LIKE @Q OR Author LIKE @Q OR ISBN LIKE @Q"
            conn.Query<Book>(sql, {| Q = "%" + query + "%" |}) |> Seq.toList
        )

// ==========================================
// 3. GUI IMPLEMENTATION
// ==========================================
type LibraryForm() as this =
    inherit Form()

    // Controls
    let grid = new DataGridView(Dock = DockStyle.Top, Height = 280, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, ReadOnly = true)
    let panelControls = new FlowLayoutPanel(Dock = DockStyle.Bottom, Height = 180, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10))
    
    // Inputs
    let txtTitle = new TextBox(PlaceholderText = "Title", Width = 150)
    let txtAuthor = new TextBox(PlaceholderText = "Author", Width = 150)
    let txtISBN = new TextBox(PlaceholderText = "ISBN", Width = 100)
    // Hidden Label to keep track of selected ID for editing
    let lblSelectedId = new Label(Text = "0", Visible = false) 
    
    // Buttons
    let btnAdd = new Button(Text = "Add / New Copy", Width = 120, BackColor = Color.LightGreen)
    let btnUpdateInfo = new Button(Text = "Update Info", Width = 120, BackColor = Color.LightYellow)
    let btnDelete = new Button(Text = "Delete Book", Width = 100, BackColor = Color.LightCoral) // زر الحذف
    
    let btnBorrow = new Button(Text = "Borrow", Width = 80)
    let btnReturn = new Button(Text = "Return", Width = 80)
    
    let txtSearch = new TextBox(PlaceholderText = "Search...", Width = 150)
    let btnSearch = new Button(Text = "Search", Width = 80)
    let btnRefresh = new Button(Text = "Refresh", Width = 80)

    let refreshGrid (data: Book list) =
        let bindingList = new System.ComponentModel.BindingList<Book>(ResizeArray<Book>(data))
        grid.DataSource <- bindingList

    do
        this.Text <- "Library System (CRUD + GUI)"
        this.Size <- Size(850, 550)
        this.StartPosition <- FormStartPosition.CenterScreen

        // Layout
        panelControls.Controls.Add(new Label(Text = "Title:", AutoSize = true))
        panelControls.Controls.Add(txtTitle)
        panelControls.Controls.Add(new Label(Text = "Author:", AutoSize = true))
        panelControls.Controls.Add(txtAuthor)
        panelControls.Controls.Add(new Label(Text = "ISBN:", AutoSize = true))
        panelControls.Controls.Add(txtISBN)
        
        // Operation Buttons
        panelControls.Controls.Add(btnAdd)
        panelControls.Controls.Add(btnUpdateInfo)
        panelControls.Controls.Add(btnDelete)
        
        // Spacer
        panelControls.Controls.Add(new Panel(Width = 800, Height = 10))

        // Action Buttons
        panelControls.Controls.Add(btnBorrow)
        panelControls.Controls.Add(btnReturn)
        panelControls.Controls.Add(txtSearch)
        panelControls.Controls.Add(btnSearch)
        panelControls.Controls.Add(btnRefresh)

        this.Controls.Add(grid)
        this.Controls.Add(panelControls)

        // Events
        this.Load.Add(fun _ -> 
            Backend.initializeDatabase()
            refreshGrid (Backend.getAllBooks())
        )

        // تعبئة الخانات عند الضغط على سطر في الجدول
        grid.SelectionChanged.Add(fun _ -> 
            if grid.SelectedRows.Count > 0 then
                let row = grid.SelectedRows.[0]
                txtTitle.Text <- string row.Cells.["Title"].Value
                txtAuthor.Text <- string row.Cells.["Author"].Value
                txtISBN.Text <- string row.Cells.["ISBN"].Value
                lblSelectedId.Text <- string row.Cells.["Id"].Value
        )

        // 1. ADD BOOK
        btnAdd.Click.Add(fun _ -> 
            if txtTitle.Text <> "" then
                let msg = Backend.addOrUpdateBook txtTitle.Text txtAuthor.Text txtISBN.Text
                MessageBox.Show(msg) |> ignore
                refreshGrid (Backend.getAllBooks())
        )

        // 2. UPDATE INFO (تعديل المعلومات)
        btnUpdateInfo.Click.Add(fun _ -> 
            if lblSelectedId.Text <> "0" then
                let id = int lblSelectedId.Text
                let msg = Backend.updateBookDetails id txtTitle.Text txtAuthor.Text txtISBN.Text
                MessageBox.Show(msg) |> ignore
                refreshGrid (Backend.getAllBooks())
            else
                MessageBox.Show("Select a book from the list first") |> ignore
        )

        // 3. DELETE BOOK (الحذف)
        btnDelete.Click.Add(fun _ -> 
            if lblSelectedId.Text <> "0" then
                let confirm = MessageBox.Show("Are you sure you want to delete this book?", "Confirm", MessageBoxButtons.YesNo)
                if confirm = DialogResult.Yes then
                    let id = int lblSelectedId.Text
                    let msg = Backend.deleteBook id
                    MessageBox.Show(msg) |> ignore
                    refreshGrid (Backend.getAllBooks())
                    txtTitle.Clear(); txtAuthor.Clear(); txtISBN.Clear(); lblSelectedId.Text <- "0"
            else
                MessageBox.Show("Select a book to delete") |> ignore
        )

        // Borrow & Return uses ID now for accuracy
        btnBorrow.Click.Add(fun _ -> 
             if lblSelectedId.Text <> "0" then
                let msg = Backend.borrowBook (int lblSelectedId.Text)
                MessageBox.Show(msg) |> ignore
                refreshGrid (Backend.getAllBooks())
        )

        btnReturn.Click.Add(fun _ -> 
             if lblSelectedId.Text <> "0" then
                let msg = Backend.returnBook (int lblSelectedId.Text)
                MessageBox.Show(msg) |> ignore
                refreshGrid (Backend.getAllBooks())
        )

        btnSearch.Click.Add(fun _ -> 
            refreshGrid (Backend.searchBooks txtSearch.Text)
        )
        
        btnRefresh.Click.Add(fun _ -> refreshGrid (Backend.getAllBooks()))

module Program =
    [<STAThread>]
    [<EntryPoint>]
    let main argv =
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(false)
        Application.Run(new LibraryForm())
        0
