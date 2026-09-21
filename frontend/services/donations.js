import request from "../lib/request";

export const redeemRewarbleVoucher = ({ code }) => {
  return request("POST", "/donation-api/rewarble/redeem", { code }).then(response => response.data);
};
