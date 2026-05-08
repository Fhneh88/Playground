using System;
using System.Collections.Generic;

public class Book
{
    public string Title { get; }
    public string Author { get; }
    public int Year { get; }
    public bool IsAvailable { get; set; }

    public Book(string title, string author, int year)
    {
        Title = title;
        Author = author;
        Year = year;
        IsAvailable = true;
    }

    public string GetInfo()
    {
        return $"{Author} - {Title} ({Year})";
    }
}

public class Library
{
    private List<Book> books = new List<Book>();

    public void AddBook(Book book)
    {
        books.Add(book);
    }

    public List<Book> FindByAuthor(string author)
    {
        return books.FindAll(book => book.Author == author);
    }

    public bool BorrowBook(string title)
    {
        Book book = books.Find(b => b.Title == title);

        if (book != null && book.IsAvailable)
        {
            book.IsAvailable = false;
            return true;
        }

        return false;
    }

    public List<Book> GetAvailableBooks()
    {
        return books.FindAll(book => book.IsAvailable);
    }
}