# Golf App

A comprehensive golf tournament organizer application for managing, scoring, and viewing golf tournaments.

## Features

- Tournament creation and management
- Mobile score entry with offline capability
- Real-time leaderboards and statistics
- Player and flight management
- Multi-language support (English and Dutch)
- Track golf courses across the Netherlands and globally
- View detailed information about courses, tees, and holes

## Data Structure

The application uses a hierarchical data structure:
- Golf Clubs
  - Courses
    - Tees
      - Holes
      - Playing Handicaps (PHCP)

## API Integration

The app integrates with the e-golf4u API to fetch course data.

## Setup & Usage

1. Clone the repository
2. Run the fetch_golf_data.ps1 script to update course data
3. Open the app and explore golf tournaments

## Multi-Language Support

The application fully supports:
- English (default)
- Dutch

Community translations can be contributed following the guidelines in our [multi-language strategy](docs/multi-language-strategy.md).

## License

[MIT License](LICENSE)
