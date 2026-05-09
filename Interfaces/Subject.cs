namespace EcommerceSystem.Interfaces;

public interface Subject
{
    void Attach(Observer observer);
    void Detach(Observer observer);
    void NotifyObservers();
}

