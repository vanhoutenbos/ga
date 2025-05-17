// i18n.js - Configuration for internationalization

import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import Backend from 'i18next-http-backend';

i18n
  // Load translations from server/CDN
  .use(Backend)
  
  // Detect user language
  .use(LanguageDetector)
  
  // Pass i18n instance to react-i18next
  .use(initReactI18next)
  
  // Initialize i18next
  .init({
    // Default language
    fallbackLng: 'en',
    
    // Debug mode (disable in production)
    debug: process.env.NODE_ENV !== 'production',
    
    // Namespace configuration
    ns: ['common', 'tournaments', 'scoring', 'players', 'leaderboard', 'courses', 'settings', 'auth'],
    defaultNS: 'common',
    
    // Backend configuration
    backend: {
      // Path to load translations from
      loadPath: '/locales/{{lng}}/{{ns}}.json',
    },
    
    // Language detection options
    detection: {
      // Order of detection methods
      order: ['localStorage', 'cookie', 'navigator', 'htmlTag'],
      
      // Cache language selection
      caches: ['localStorage', 'cookie'],
      
      // Cookie options
      cookieExpirationDate: new Date(new Date().getFullYear() + 1, 0, 1), // 1 year
      cookieDomain: window.location.hostname,
    },
    
    // Interpolation options
    interpolation: {
      // Escaping for security against XSS attacks
      escapeValue: false,
      // Format options for dates, numbers, etc.
      format: (value, format, lng) => {
        if (format === 'uppercase') return value.toUpperCase();
        if (value instanceof Date) {
          const options = {};
          
          // Handle date formats
          if (format.includes('YYYY')) {
            // Use language-appropriate date formatting
            const dateFormat = new Intl.DateTimeFormat(lng, {
              year: 'numeric',
              month: format.includes('MMMM') ? 'long' : 'short',
              day: 'numeric'
            });
            return dateFormat.format(value);
          }
          
          // Handle time formats
          if (format.includes('HH') || format.includes('h:mm')) {
            const timeFormat = new Intl.DateTimeFormat(lng, {
              hour: '2-digit',
              minute: '2-digit',
              hour12: lng === 'en' // Use 12-hour format for English, 24-hour for others
            });
            return timeFormat.format(value);
          }
        }
        
        // Handle number formatting
        if (typeof value === 'number') {
          const options = {};
          
          if (format === 'currency') {
            options.style = 'currency';
            options.currency = lng === 'en' ? 'USD' : 'EUR';
          }
          
          if (format === 'percent') {
            options.style = 'percent';
            options.minimumFractionDigits = 1;
            options.maximumFractionDigits = 2;
          }
          
          return new Intl.NumberFormat(lng, options).format(value);
        }
        
        return value;
      }
    },
    
    // React options
    react: {
      useSuspense: true,
      bindI18n: 'languageChanged loaded',
      bindI18nStore: 'added removed',
      nsMode: 'default'
    }
  });

export default i18n;
