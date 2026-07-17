require 'rails_helper'

RSpec.describe 'OIDC callback against the local GOV.UK One Login simulator', type: :request do
  let(:base_uri)  { Rails.application.config.gov_one_base_uri.to_s.chomp('/') }
  let(:client_id) { Rails.application.config.gov_one_client_id }

  let(:auth_service) { instance_double(GovOneAuthService) }
  let(:access_token) { 'simulator_access_token' }
  let(:id_token)     { 'simulator_id_token' }
  let(:gov_one_id)   { 'simulator-sub-12345' }

  # Predictable values so we can replay them on the callback request
  let(:state) { '00000000-0000-0000-0000-000000000000' }
  let(:nonce) { 'AAAAAAAAAAAAAAAAAAAAAAAAA' }

  let(:email) { 'sim.user@example.com' }

  let(:decoded_id_token) do
    {
      'sub' => gov_one_id,
      'nonce' => nonce,
      'iss' => base_uri,
      'aud' => client_id,
    }
  end

  before do
    allow(SecureRandom).to receive(:uuid).and_return(state)
    allow(SecureRandom).to receive(:alphanumeric).and_call_original
    allow(SecureRandom).to receive(:alphanumeric).with(25).and_return(nonce)

    allow(GovOneAuthService).to receive(:new).and_return(auth_service)
    allow(auth_service).to receive(:tokens).and_return(
      'access_token' => access_token,
      'id_token' => id_token,
    )
    allow(auth_service).to receive(:decode_id_token).with(id_token).and_return([decoded_id_token])
    allow(auth_service).to receive(:user_info).with(access_token).and_return(
      'sub' => gov_one_id,
      'email' => email,
    )

    get '/users/sign-in'
  end

  def session_cookie
    cookies[Rails.application.config.session_options.fetch(:key)]
  end

  context 'with a brand-new user' do
    it 'creates the user, signs them in and routes to terms-and-conditions' do
      expect {
        get '/users/auth/openid_connect/callback', params: { code: 'sim-code', state: state }
      }.to change(User, :count).by(1)

      created = User.find_by(email: email)
      expect(created).to be_present
      expect(created.gov_one_id).to eq(gov_one_id)
      expect(response).to redirect_to(edit_registration_terms_and_conditions_path)
    end

    it 'rotates the pre-authentication session on successful authentication' do
      pre_authentication_cookie = session_cookie

      get '/users/auth/openid_connect/callback', params: { code: 'sim-code', state: state }

      expect(session_cookie).to be_present
      expect(session_cookie).not_to eq(pre_authentication_cookie)
    end
  end

  context 'with an existing pre-gov-one user (matched by email)' do
    before { create(:user, :registered, email: email, gov_one_id: nil) }

    it 'links the GOV.UK One Login id to the existing account and routes to my-modules' do
      expect {
        get '/users/auth/openid_connect/callback', params: { code: 'sim-code', state: state }
      }.not_to change(User, :count)

      expect(User.find_by(email: email).gov_one_id).to eq(gov_one_id)
      expect(response).to redirect_to(my_modules_path)
    end
  end

  context 'with an existing gov-one user (matched by sub)' do
    before { create(:user, :registered, gov_one_id: gov_one_id) }

    it 'signs them in and routes to my-modules' do
      get '/users/auth/openid_connect/callback', params: { code: 'sim-code', state: state }
      expect(response).to redirect_to(my_modules_path)
    end
  end

  context 'when One Login returns an error or the correlation state is invalid' do
    it 'rejects a provider cancellation without creating or authenticating a user' do
      expect {
        get '/users/auth/openid_connect/callback', params: {
          error: 'access_denied',
          error_description: 'The user cancelled sign in',
          state: state,
        }
      }.not_to change(User, :count)

      expect(response).to redirect_to(root_path)
      follow_redirect!
      expect(response.body).to include('There was a problem signing in. Please try again.')
      get my_modules_path
      expect(response).to redirect_to(new_user_session_path)
    end

    it 'rejects a mismatched state before exchanging the authorization code' do
      get '/users/auth/openid_connect/callback', params: {
        code: 'sim-code',
        state: 'attacker-controlled-state',
      }

      expect(auth_service).not_to have_received(:tokens)
      expect(response).to redirect_to(root_path)
      expect(User.find_by(email: email)).to be_nil
    end

    it 'rejects a missing state before exchanging the authorization code' do
      get '/users/auth/openid_connect/callback', params: { code: 'sim-code' }

      expect(auth_service).not_to have_received(:tokens)
      expect(response).to redirect_to(root_path)
      expect(User.find_by(email: email)).to be_nil
    end
  end
end
