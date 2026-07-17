require 'rails_helper'

RSpec.describe 'Rails session contract', type: :request do
  let(:session_cookie_key) { Rails.application.config.session_options.fetch(:key) }

  def current_session_cookie
    cookies[session_cookie_key]
  end

  it 'issues an HTTP-only same-site cookie and leaves Secure environment-derived' do
    get new_user_session_path

    set_cookie = Array(response.headers['Set-Cookie']).find do |value|
      value.start_with?("#{session_cookie_key}=")
    end
    expect(set_cookie).to be_present
    expect(set_cookie).to include('path=/')
      .and match(/httponly/i)
      .and match(/samesite=lax/i)
    expect(set_cookie).not_to match(/;\s*secure/i)
  end

  it 'rejects a tampered session cookie without authenticating or raising an error' do
    get new_user_session_path
    original_cookie = current_session_cookie
    expect(original_cookie).to be_present

    replacement = original_cookie.end_with?('a') ? 'b' : 'a'
    cookies[session_cookie_key] = "#{original_cookie[0...-1]}#{replacement}"

    get my_modules_path

    expect(response).to redirect_to(new_user_session_path)
    expect(response).not_to have_http_status(:internal_server_error)
  end

  it 'enforces the timeout loaded from TIMEOUT_IN_MINUTES on a subsequent request' do
    expect(Devise.timeout_in).to eq(Rails.configuration.timeout_in_minutes.minutes)

    user = create(:user, :registered)
    travel_to(Time.zone.local(2026, 7, 16, 12)) do
      sign_in user
      get my_modules_path
      expect(response).to have_http_status(:success)
    end

    travel_to(Time.zone.local(2026, 7, 16, 12) + Devise.timeout_in + 1.second) do
      get my_modules_path
      expect(response).to redirect_to(my_modules_path)
      follow_redirect!
      expect(response).to redirect_to(new_user_session_path)
    end
  end

  it 'invalidates the browser cookie on local sign-out but characterizes old CookieStore replay' do
    user = create(:user, :registered)
    sign_in user
    get my_modules_path
    pre_logout_cookie = current_session_cookie
    expect(pre_logout_cookie).to be_present

    get destroy_user_session_path
    expect(response).to redirect_to(root_path)
    post_logout_cookie = current_session_cookie
    expect(post_logout_cookie).to be_present
    expect(post_logout_cookie).not_to eq(pre_logout_cookie)

    get my_modules_path
    expect(response).to redirect_to(new_user_session_path)

    # Rails CookieStore has no server-side revocation record. Replaying the
    # still-valid pre-logout bearer cookie restores the old Warden identity.
    cookies[session_cookie_key] = pre_logout_cookie
    get my_modules_path
    expect(response).to have_http_status(:success)
  end
end
