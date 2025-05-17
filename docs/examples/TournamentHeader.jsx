// Sample React component showing how to use translations

import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDate, formatNumber } from '../utils/formatting';

/**
 * Tournament Header component that displays tournament information
 * with appropriate internationalization
 */
const TournamentHeader = ({ tournament }) => {
  const { t, i18n } = useTranslation(['tournaments', 'common']);
  const [currentLanguage, setCurrentLanguage] = useState(i18n.language);
  
  // Format dates according to the current language
  const formattedStartDate = formatDate(tournament.startDate, i18n.language);
  const formattedEndDate = formatDate(tournament.endDate, i18n.language);
  
  // Handle language change
  const changeLanguage = (language) => {
    i18n.changeLanguage(language);
    setCurrentLanguage(language);
  };
  
  return (
    <div className="tournament-header">
      <div className="tournament-title">
        <h1>{tournament.name}</h1>
        <div className="tournament-dates">
          {t('tournaments:dateRange', { 
            startDate: formattedStartDate, 
            endDate: formattedEndDate 
          })}
        </div>
      </div>
      
      <div className="tournament-details">
        <div className="detail-item">
          <span className="label">{t('tournaments:location')}:</span>
          <span className="value">{tournament.location}</span>
        </div>
        
        <div className="detail-item">
          <span className="label">{t('tournaments:format')}:</span>
          <span className="value">
            {t(`tournaments:formatTypes.${tournament.format}`)}
          </span>
        </div>
        
        <div className="detail-item">
          <span className="label">{t('tournaments:organizer')}:</span>
          <span className="value">{tournament.organizer}</span>
        </div>
        
        <div className="detail-item">
          <span className="label">{t('tournaments:status')}:</span>
          <span className="value status-badge">
            {t(`tournaments:status.${tournament.status}`)}
          </span>
        </div>
      </div>
      
      <div className="language-selector">
        <label htmlFor="language-select">{t('common:languages.language')}:</label>
        <select 
          id="language-select"
          value={currentLanguage} 
          onChange={(e) => changeLanguage(e.target.value)}
        >
          <option value="en">{t('common:languages.en')}</option>
          <option value="nl">{t('common:languages.nl')}</option>
        </select>
      </div>
    </div>
  );
};

export default TournamentHeader;
