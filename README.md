# Minesweeper Infinite Multiplayer

Ambitniejsza wersja klasycznego Sapera z trybem kooperacji dla wielu graczy, stworzona w **C#** przy użyciu frameworka **MonoGame**. W przeciwieństwie do tradycyjnej gry, projekt ten oferuje nieskończoną, proceduralnie generowaną planszę opartą na systemie chunków.

## Funkcje

*   **Nieskończony Świat**: Graj na mapie, która rozszerza się wraz z Twoją eksploracją, zarządzaną przez wydajną strukturę danych.
*   **Multiplayer Co-op**: Hostuj serwer lub dołączaj do znajomych przez IP, aby wspólnie oczyszczać pole minowe.
*   **Synchronizacja w czasie rzeczywistym**: Widzisz kursory innych graczy oraz ich postępy na żywo.
*   **Dynamiczna Kamera**: Płynne przybliżanie (zoom) i przesuwanie widoku, ułatwiające nawigację po gigantycznej siatce.
*   **Generator oparty na Seedach**: Wykorzystuje algorytm SplitMix64, co zapewnia deterministyczne i sprawiedliwe rozmieszczenie min.

## Sterowanie

### Myszka
*   **LPM**: Odkrycie pola.
*   **PPM**: Postawienie/zdjęcie flagi.
*   **Scroll**: Przesuwanie góra/dół.
*   **Shift + Scroll**: Przesuwanie lewo/prawo.
*   **Ctrl + Scroll**: Przybliżanie i oddalanie widoku (Zoom).

### Klawiatura
*   **WASD**: Poruszanie kamerą.
*   **ESC**: Natychmiastowy powrót do menu głównego z dowolnego ekranu gry.

## Mechanika Gry
*   **Lobby**: Przed startem gracze mogą wybrać swój unikalny kolor kursora.
*   **Ochrona pierwszego kliknięcia**: Pierwszy ruch w nowej grze nigdy nie trafi na minę i zawsze otwiera bezpieczny obszar (min. 3x3).
*   **Chording**: Kliknięcie lewym przyciskiem na odkrytą cyfrę (jeśli liczba flag wokół się zgadza) automatycznie odkrywa sąsiednie, nieoznaczone pola.
*   **Reset**: Host może w dowolnym momencie zresetować grę.

## Wymagania
*   .NET 6.0 SDK lub nowszy
*   Biblioteki MonoGame 3.8.1+

## Struktura Projektu
*   `Board` & `BoardData`: Logika nieskończonej siatki i zarządzanie stanem pól.
*   `Chunks`: System optymalizacji pamięci. Pamiętane są tylko odkryte pola. Mapa jest generowana w locie.
*   `Multiplayer`: Moduły `ServerManager` i `NetworkManager` obsługujące komunikację TCP.
*   `Camera`: Obsługa transformacji widoku i zoomu.

---

### Jak uruchomić
1. Sklonuj repozytorium.
2. Otwórz projekt.
3. Upewnij się, że nie brakuje assetów oraz pliki w `Content.mgcb` są zbudowane (Pipeline Tool).
4. Uruchom projekt.
