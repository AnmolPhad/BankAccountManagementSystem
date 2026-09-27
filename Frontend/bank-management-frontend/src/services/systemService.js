import api from '../api';

/**
 * Service for administrative system backup (save) and restore operations (/api/v1/system).
 */
export const systemService = {
  /**
   * Exports a complete system backup JSON object from the backend.
   * Requires Admin authorization.
   * @returns {Promise<Object>} SystemBackupDto payload
   */
  async createBackup() {
    const response = await api.get('/system/save');
    return response.data;
  },

  /**
   * Restores accounts and transactions from a system backup payload.
   * Requires Admin authorization.
   * @param {Object} backupPayload - SystemBackupDto object
   * @returns {Promise<Object>} RestoreResponseDto payload
   */
  async restoreBackup(backupPayload) {
    const response = await api.post('/system/restore', backupPayload);
    return response.data;
  },
};

export default systemService;
