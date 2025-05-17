# Translation Contribution Guide

This guide explains how the community can contribute translations to expand the language support for the Golf Tournament Organizer Application.

## Translation Workflow

### 1. Getting Started

To contribute translations:

1. **Fork the Repository**: Create your own fork of the main repository on GitHub
2. **Clone the Repository**: `git clone https://github.com/yourusername/the-golfapp.git`
3. **Create a Branch**: `git checkout -b add-language-[code]` (replace [code] with the ISO language code, e.g., 'de' for German)

### 2. Adding a New Language

Each language has its own folder in the `/public/locales/` directory, containing JSON files for each feature area.

#### Steps for Adding a New Language:

1. Create a new folder for your language in `/public/locales/` using the ISO language code (e.g., 'de' for German)
2. Copy all the JSON files from the English (`en`) folder to your new language folder
3. Translate each JSON file, keeping the key structure intact and only changing the values

Example structure for German translations:
```
/public/locales/
  ├── en/                  # English (reference)
  │   ├── common.json
  │   ├── tournaments.json
  │   └── ...
  └── de/                  # German (new language)
      ├── common.json
      ├── tournaments.json
      └── ...
```

### 3. Translation Guidelines

Follow these guidelines when translating:

1. **Maintain Placeholders**: Keep all variables like `{{name}}` or `{{date}}` intact
2. **Preserve Context**: Understand the context in which each string is used
3. **Respect Formatting**: Preserve any HTML tags or special formatting
4. **Be Consistent**: Use consistent terminology throughout all files
5. **Keep a Natural Tone**: Translations should sound natural in the target language
6. **Consider Space Constraints**: Some languages may require more space than English

### 4. Testing Your Translations

Before submitting, test your translations:

1. Place your translation files in the correct directories
2. Run the application locally
3. Switch to your new language
4. Navigate through different features to ensure all translations display correctly
5. Check that date formats, number formats, and layouts work correctly

### 5. Submitting Your Contribution

When your translations are ready:

1. Commit your changes: `git commit -m "Add [language] translations"`
2. Push to your fork: `git push origin add-language-[code]`
3. Create a Pull Request (PR) to the main repository
4. In the PR description, mention:
   - The language you've added
   - Any specific cultural considerations
   - Any areas that need further review

### 6. Review Process

After submission:

1. Native speakers will review your translations
2. Automated checks will verify file structure and formatting
3. Feedback will be provided if changes are needed
4. Once approved, your translations will be merged into the main branch

## Updating Existing Translations

To update or improve existing translations:

1. Follow the same fork and clone process
2. Make your improvements to the existing files
3. Test your changes
4. Submit a PR with a clear description of what you've improved

## Translation Status Board

Track the status of language translations on our [Translation Status Board](https://golfapp.com/translation-status) (accessible after launch).

## Recognition

All translation contributors will be:

1. Credited in the application's "About" section
2. Listed in the CONTRIBUTORS.md file
3. Recognized on the project's website

## Questions or Help

If you need assistance with translations:

- Join our [Discord community](https://discord.gg/golfapp)
- Post in the #translations channel
- Review the [FAQ for Translators](https://golfapp.com/translation-faq)

Thank you for helping make the Golf Tournament Organizer accessible to more people around the world!

---

*Last updated: May 17, 2025*
