# Minesweeper Infinite Multiplayer

Zaawansowana wersja klasycznego Sapera z trybem kooperacji dla wielu graczy, stworzona w **C#** przy użyciu frameworka **MonoGame**. W przeciwieństwie do tradycyjnej gry, projekt ten oferuje nieskończoną, proceduralnie generowaną planszę opartą na systemie chunków.

## Funkcje

*   **Nieskończony Świat**: Graj na mapie, która rozszerza się wraz z Twoją eksploracją, zarządzaną przez wydajną strukturę danych.
*   **Multiplayer Co-op**: Hostuj serwer lub dołączaj do znajomych przez IP, aby wspólnie oczyszczać pole minowe.
*   **Synchronizacja w czasie rzeczywistym**: Widzisz kursory innych graczy oraz ich postępy na żywo.
*   **Dynamiczna Kamera**: Płynne przybliżanie (zoom) i przesuwanie widoku, ułatwiające nawigację po gigantycznej siatce.
*   **Generator oparty na Seedach**: Wykorzystuje algorytm SplitMix64, co zapewnia deterministyczne i sprawiedliwe rozmieszczenie min.

## Sterowanie

### Myszka
*   **LPM (Lewy Przycisk)**: Odkrycie pola.
*   **PPM (Prawy Przycisk)**: Postawienie/zdjęcie flagi.
*   **ŚPM (Środkowy Przycisk - Przytrzymanie)**: Swobodne przesuwanie kamery.
*   **Rolka myszy**: Przesuwanie góra/dół (Shift + Rolka dla przesunięcia lewo/prawo).
*   **Ctrl + Rolka myszy**: Przybliżanie i oddalanie widoku (Zoom).

### Klawiatura
*   **W / A / S / D**: Poruszanie kamerą.
*   **ESC**: Natychmiastowy powrót do menu głównego z dowolnego ekranu gry.

## Mechanika Gry
*   **Lobby**: Przed startem gracze mogą wybrać swój unikalny kolor kursora.
*   **Ochrona pierwszego kliknięcia**: Pierwszy ruch w nowej grze nigdy nie trafi na minę i zawsze otwiera bezpieczny obszar.
*   **Chording**: Kliknięcie lewym przyciskiem na odkrytą cyfrę (jeśli liczba flag wokół się zgadza) automatycznie odkrywa sąsiednie, nieoznaczone pola.
*   **Reset**: Host może w dowolnym momencie zresetować grę przyciskiem "RESET" na górnym pasku.

## Wymagania
*   .NET 6.0 SDK lub nowszy
*   Biblioteki MonoGame 3.8.1+

## Struktura Projektu
*   `Board` & `BoardData`: Logika nieskończonej siatki i zarządzanie stanem pól.
*   `Chunks`: System optymalizacji pamięci i renderowania fragmentów mapy.
*   `Multiplayer`: Moduły `ServerManager` i `NetworkManager` obsługujące komunikację TCP.
*   `Camera`: Obsługa transformacji widoku i zoomu.

---

### Jak uruchomić
1. Sklonuj repozytorium.
2. Otwórz projekt w **Visual Studio 2022**.
3. Upewnij się, że pliki w `Content.mgcb` są zbudowane (Pipeline Tool).
4. Uruchom projekt (F5).
