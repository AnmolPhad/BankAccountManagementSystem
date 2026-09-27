import api from '../api';

/**
 * Service for financial transaction operations (/api/v1/transactions).
 */
export const transactionService = {
  /**
   * Deposits cash into a bank account.
   * @param {Object} params - { accountId, amount, description }
   */
  async deposit({ accountId, amount, description }) {
    const response = await api.post('/transactions/deposit', {
      accountId,
      amount: Number(amount),
      description: description?.trim() || undefined,
    });
    return response.data;
  },

  /**
   * Withdraws cash from a bank account.
   * @param {Object} params - { accountId, amount, description }
   */
  async withdraw({ accountId, amount, description }) {
    const response = await api.post('/transactions/withdraw', {
      accountId,
      amount: Number(amount),
      description: description?.trim() || undefined,
    });
    return response.data;
  },

  /**
   * Performs a cheque transfer from a Checking account to a destination account.
   * @param {Object} params - { sourceAccountId, destinationAccountId, amount, description }
   */
  async transfer({ sourceAccountId, destinationAccountId, amount, description }) {
    const response = await api.post('/transactions/transfer', {
      sourceAccountId,
      destinationAccountId,
      amount: Number(amount),
      description: description?.trim() || undefined,
    });
    return response.data;
  },

  /**
   * Looks up an account by its customer-facing 10-digit account number.
   * Used to resolve destination account GUIDs for cheque transfers.
   * @param {string} accountNumber
   */
  async getAccountByNumber(accountNumber) {
    const response = await api.get(`/accounts/by-number/${accountNumber.trim()}`);
    return response.data;
  },

  /**
   * Retrieves transaction history for accounts.
   * Supports filtering by accountId, date range (fromDate, toDate), lastN, and pagination.
   * @param {Object} params
   * @returns {Promise<Object>} PagedResult<TransactionResponse>
   */
  async getTransactions(params = {}) {
    const queryParams = new URLSearchParams();
    if (params.accountId) queryParams.append('accountId', params.accountId);
    if (params.fromDate) queryParams.append('fromDate', params.fromDate);
    if (params.toDate) queryParams.append('toDate', params.toDate);
    if (params.lastN) queryParams.append('lastN', params.lastN);
    if (params.transactionType) queryParams.append('transactionType', params.transactionType);
    if (params.transactionMode) queryParams.append('transactionMode', params.transactionMode);
    if (params.page) queryParams.append('page', params.page);
    if (params.pageSize) queryParams.append('pageSize', params.pageSize);

    const queryString = queryParams.toString();
    const url = queryString ? `/transactions?${queryString}` : '/transactions';
    const response = await api.get(url);
    return response.data;
  },
};

export default transactionService;
