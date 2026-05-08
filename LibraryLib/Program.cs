var library = new Library();

        library.AddBook(new Book("Война и мир", "Толстой", 1869));
        library.AddBook(new Book("Анна Каренина", "Толстой", 1877));
        library.AddBook(new Book("Преступление и наказание", "Достоевский", 1866));

        Console.WriteLine("Все книги Толстого:");
        var tolstoyBooks = library.FindByAuthor("Толстой");
        foreach (var book in tolstoyBooks)
        {
            Console.WriteLine(book.GetInfo());
        }
        Console.WriteLine($"Количество книг Толстого: {tolstoyBooks.Count}");
        Console.WriteLine();

        Console.WriteLine("Берём книгу 'Война и мир':");
        Console.WriteLine(library.BorrowBook("Война и мир")); // true
        Console.WriteLine("Пробуем взять её ещё раз:");
        Console.WriteLine(library.BorrowBook("Война и мир")); // false
        Console.WriteLine();

        Console.WriteLine("Доступные книги:");
        var availableBooks = library.GetAvailableBooks();
        foreach (var book in availableBooks)
        {
            Console.WriteLine(book.GetInfo());
        }
        Console.WriteLine($"Количество доступных книг: {availableBooks.Count}");