import api from '../api';

/**
 * Service for user profile operations (/api/v1/users).
 */
export const userService = {
  /**
   * Retrieves user profile details by User ID.
   * @param {string} id - User ID
   * @returns {Promise<Object>} UserResponse object
   */
  async getUserById(id) {
    const response = await api.get(`/users/${id}`);
    return response.data;
  },

  /**
   * Updates user personal profile information (FirstName, LastName, Phone).
   * @param {string} id - User ID
   * @param {Object} data - { firstName, lastName, phone }
   * @returns {Promise<Object>} Response object
   */
  async updateProfile(id, data) {
    const response = await api.put(`/users/${id}`, {
      firstName: data.firstName?.trim() || undefined,
      lastName: data.lastName?.trim() || undefined,
      phone: data.phone?.trim() || undefined,
    });
    return response.data;
  },
};

export default userService;
