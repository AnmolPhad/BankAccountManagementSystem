#include <iostream>
#include <fstream>
#include <string>
#include <regex>
#include <cmath>

using namespace std;

void writeShapesToFile()
{
    ofstream file("shapes.txt");

    if (file)
    {
        if (!file.is_open())
        {
            cout << "Unable to open file!" << endl;
            return;
        }

        file << "Circle" << endl;
        file << "Radius: 5" << endl;

        file << endl;

        file << "Rectangle" << endl;
        file << "Length: 10" << endl;
        file << "Width: 20" << endl;

        file << endl;

        file << "Square" << endl;
        file << "Side: 6" << endl;

        file << endl;

        file << "Triangle" << endl;
        file << "Side1: 3" << endl;
        file << "Side2: 4" << endl;
        file << "Side3: 5" << endl;

        file.close();

        cout << "Shape information written successfully." << endl;
    }
}

class Shape
{
public:
    virtual void display() = 0;
    virtual double area() = 0;
    virtual double perimeter() = 0;

    virtual ~Shape()
    {
    }
};

class Circle : public Shape
{
private:
    double radius;

public:
    Circle(double r)
    {
        radius = r;
    }

    void display() override
    {
        cout << "Circle" << endl;
        cout << "Radius: " << radius << endl;
    }

    double area() override
    {
        return 3.14 * radius * radius;
    }

    double perimeter() override
    {
        return 2 * 3.14 * radius;
    }
};

class Rectangle : public Shape
{
private:
    double length;
    double width;

public:
    Rectangle(double l, double w)
    {
        length = l;
        width = w;
    }

    void display() override
    {
        cout << "Rectangle" << endl;
        cout << "Length: " << length << endl;
        cout << "Width: " << width << endl;
    }

    double area() override
    {
        return length * width;
    }

    double perimeter() override
    {
        return 2 * (length + width);
    }
};

class Square : public Rectangle
{
private:
    double side;

public:
    Square(double s) : Rectangle(s, s)
    {
        side = s;
    }

    void display() override
    {
        cout << "Square" << endl;
        cout << "Side: " << side << endl;
    }
};

class Triangle : public Shape
{
private:
    double side1;
    double side2;
    double side3;

public:
    Triangle(double s1, double s2, double s3)
    {
        side1 = s1;
        side2 = s2;
        side3 = s3;
    }

    void display() override
    {
        cout << "Triangle" << endl;
        cout << "Side1: " << side1 << endl;
        cout << "Side2: " << side2 << endl;
        cout << "Side3: " << side3 << endl;
    }

    double perimeter() override
    {
        return side1 + side2 + side3;
    }

    double area() override
    {
        double s = perimeter() / 2;

        return sqrt(s * (s - side1) *
                    (s - side2) *
                    (s - side3));
    }
};

int menu(void)
{
    cout << endl
         << "0.EXIT" << endl
         << "1.Circle" << endl
         << "2.Rectangle" << endl
         << "3.Triangle" << endl
         << "4.Square" << endl;

    int choice = 0;
    cout << "Enter your choice: ";
    cin >> choice;
    return choice;
}

double getValue(string line)
{
    regex pattern("(\\d+(\\.\\d+)?)");

    smatch match;

    if (regex_search(line, match, pattern))
    {
        return stod(match[1]);
    }

    return 0;
}

int main(void)
{
    writeShapesToFile();
    ifstream file("shapes.txt");

    if (!file.is_open())
    {
        cout << "Unable to open file!" << endl;
        return 1;
    }

    Shape *sh = nullptr;

    int choice = 0;

    while ((choice = menu()) != 0)
    {
        switch (choice)
        {
        case 1:
        {
            string line;
            while (getline(file, line))
            {
                if (regex_match(line, regex("^Circle$")))
                {
                    getline(file, line);
                    double radius = getValue(line);

                    sh = new Circle(radius);
                    sh->display();
                    cout << "Area: " << sh->area() << endl;
                    cout << "Perimeter: " << sh->perimeter() << endl;

                    delete sh;
                    sh = nullptr;
                }
            }
            break;
            cout << "Circle not found in file";
        }
        break;

        case 2:
        {

            string line;

            while (getline(file, line))
            {
                if (regex_match(line, regex("^Rectangle$")))
                {
                    getline(file, line);
                    double length = getValue(line);
                    getline(file, line);
                    double width = getValue(line);
                    sh = new Rectangle(length, width);
                    sh->display();
                    cout << "Area: " << sh->area() << endl;
                    cout << "Perimeter: " << sh->perimeter() << endl;
                }
                break;
            }
            cout << "Rectangle not found in file";
        }

        break;

        case 3:
        {
            string line;

            while (getline(file, line))
            {
                if (regex_match(line, regex("^Triangle$")))
                {
                    getline(file, line);
                    double side1 = getValue(line);
                    getline(file, line);
                    double side2 = getValue(line);
                    getline(file, line);
                    double side3 = getValue(line);
                    sh = new Triangle(side1, side2, side3);
                    sh->display();
                    cout << "Area: " << sh->area() << endl;
                    cout << "Perimeter: " << sh->perimeter() << endl;
                }
                break;
            }
            cout << "Triangle not found in file";
        }

        break;

        case 4:
        {
            string line;

            while (getline(file, line))
            {
                if (regex_match(line, regex("^Square$")))
                {
                    getline(file, line);
                    double side = getValue(line);
                    sh = new Square(side);
                    sh->display();
                    cout << "Area: " << sh->area() << endl;
                    cout << "Perimeter: " << sh->perimeter() << endl;
                }
                break;
            }
            cout << "Square not found in file";
        }

        break;
        default:
            cout << "Enter valid choice : ";
            break;
        }
    }

    file.close();

    return 0;
}